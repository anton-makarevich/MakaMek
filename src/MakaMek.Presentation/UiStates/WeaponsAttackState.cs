using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.UiStates;

public class WeaponsAttackState : IUiState, ICombatSelectionRoles
{
    private readonly BattleMapViewModel _viewModel;
    private readonly Lock _stateLock = new();
    private readonly WeaponsAttackSelectionPolicy _selectionPolicy;
    private readonly WeaponRangeHighlighter _rangeHighlighter;
    private readonly WeaponSelectionPresenter _presenter;
    private readonly WeaponsConfigurationController _configuration;
    private readonly WeaponsAttackSubmitter _submitter = new();

    public IClientGame Game { get; }

    public WeaponsAttackStep CurrentStep { get; private set; } = WeaponsAttackStep.SelectingUnit;

    public string ActionLabel => CurrentStep switch
    {
        WeaponsAttackStep.SelectingUnit => _viewModel.LocalizationService.GetString("Action_SelectUnitToFire"),
        WeaponsAttackStep.ActionSelection => _viewModel.LocalizationService.GetString("Action_SelectAction"),
        WeaponsAttackStep.WeaponsConfiguration => _viewModel.LocalizationService.GetString("Action_ConfigureWeapons"),
        WeaponsAttackStep.TargetSelection => _viewModel.LocalizationService.GetString("Action_SelectTarget"),
        _ => string.Empty
    };

    public bool IsActionRequired => this.CanHumanPlayerAct();

    public bool CanExecutePlayerAction =>
        (CurrentStep is WeaponsAttackStep.ActionSelection or WeaponsAttackStep.TargetSelection)
        && !_viewModel.AreActionsMenuOffMap;

    public string PlayerActionLabel
    {
        get
        {
            if (!IsActionRequired) return string.Empty;
            return CurrentStep switch
            {
                WeaponsAttackStep.ActionSelection => _viewModel.LocalizationService.GetString("Action_SkipAttack"),
                WeaponsAttackStep.TargetSelection => Attacker?.WeaponAttackState.SelectedWeapons.Any() == true
                    ? _viewModel.LocalizationService.GetString("Action_DeclareAttack")
                    : _viewModel.LocalizationService.GetString("Action_SkipAttack"),
                _ => string.Empty
            };
        }
    }

    public void ExecutePlayerAction()
    {
        if (!this.CanHumanPlayerAct()) return;
        if (CurrentStep == WeaponsAttackStep.ActionSelection || CurrentStep == WeaponsAttackStep.TargetSelection)
        {
            ConfirmWeaponSelections();
        }
    }

    internal WeaponsAttackState(
        BattleMapViewModel viewModel,
        WeaponsAttackSelectionPolicy? selectionPolicy = null,
        WeaponRangeAnalyzer? rangeAnalyzer = null)
    {
        Game = viewModel.Game! ?? throw new InvalidOperationException("Game is not client game");
        _viewModel = viewModel;
        _selectionPolicy = selectionPolicy ?? new WeaponsAttackSelectionPolicy();
        _rangeHighlighter = new WeaponRangeHighlighter(viewModel, rangeAnalyzer ?? new WeaponRangeAnalyzer());
        _presenter = new WeaponSelectionPresenter(viewModel, Game);
        _configuration = new WeaponsConfigurationController(viewModel, Game);
        if (Game.PhaseStepState?.ActivePlayer == null)
        {
            throw new InvalidOperationException("Active player is null");
        }
    }

    public IUnit? SelectedUnit
    {
        get => CurrentStep == WeaponsAttackStep.TargetSelection ? SelectedTarget : Attacker;
        private set
        {
            lock (_stateLock)
            {
                var outcome = _selectionPolicy.Decide(
                    CurrentStep,
                    value,
                    this.CanHumanPlayerAct(),
                    Game.PhaseStepState?.ActivePlayer.Id,
                    _rangeHighlighter.WeaponRanges.Values);
                switch (outcome)
                {
                    case WeaponsAttackSelectionOutcome.Ignore:
                        return;
                    case WeaponsAttackSelectionOutcome.SelectAttacker:
                        ApplyAttackerSelection(value!);
                        break;
                    case WeaponsAttackSelectionOutcome.SelectTarget:
                        ApplyTargetSelection(value!);
                        break;
                }

                _viewModel.NotifyStateChanged();
            }
        }
    }

    private void ApplyAttackerSelection(IUnit unit)
    {
        Attacker = unit;
        _presenter.Rebuild(unit, HandleWeaponSelection, HandleAimedShotRequest);
        CurrentStep = WeaponsAttackStep.ActionSelection;

        if (unit.CanFireWeapons)
        {
            // Highlight weapon ranges for the newly selected unit
            HighlightWeaponRanges();
        }
    }

    private void ApplyTargetSelection(IUnit unit)
    {
        SelectedTarget = unit;
        _presenter.Update(Attacker, unit, _rangeHighlighter);
        _viewModel.IsWeaponSelectionVisible = true;
    }

    public void HandleUnitSelectionFromList(IUnit? unit)
    {
        SelectedUnit = unit;
    }

    public void HandleHexSelection(Hex hex)
    {
        // Handle cancellation when a direction selector is shown (torso rotation step)
        if (CurrentStep == WeaponsAttackStep.WeaponsConfiguration)
        {
            CancelTorsoRotation();
            return;
        }

        if (CurrentStep == WeaponsAttackStep.TargetSelection)
        {
            // Cancel target selection if clicked outside weapon range (non-highlighted hex)
            if (!_rangeHighlighter.IsHexInRange(hex.Coordinates))
            {
                CancelTargetSelection();
                return;
            }
        }

        HandleUnitSelectionFromHex(hex);
    }

    private void HandleUnitSelectionFromHex(Hex hex)
    {
        if (!this.CanHumanPlayerAct()) return;

        // Two units can share a hex after a blocked domino displacement. Pick the candidate by the
        // role the current step expects instead of taking whichever comes first.
        var unit = FindUnitOnHex(hex);
        if (unit == null) return;

        lock (_stateLock)
        {
            if (CurrentStep is WeaponsAttackStep.SelectingUnit or WeaponsAttackStep.ActionSelection)
            {
                SelectAttackerFromHex(unit);
                return;
            }

            if (CurrentStep == WeaponsAttackStep.TargetSelection)
            {
                if (TrySelectTargetFromHex(unit, hex)) _viewModel.NotifyStateChanged();
                return;
            }

            _viewModel.NotifyStateChanged();
        }
    }

    private IUnit? FindUnitOnHex(Hex hex)
    {
        var activePlayerId = Game.PhaseStepState?.ActivePlayer.Id;
        var selectingAttacker = CurrentStep is WeaponsAttackStep.SelectingUnit or WeaponsAttackStep.ActionSelection;

        return _viewModel.Units.FirstOrDefault(unit =>
            unit.Position?.Coordinates == hex.Coordinates
            && (selectingAttacker
                ? unit.Owner?.Id == activePlayerId
                : unit.Owner?.Id != activePlayerId));
    }

    /// <summary>
    /// Replaces the attacker with the unit under the cursor, dropping the previous attacker's
    /// selections and highlights first.
    /// </summary>
    private void SelectAttackerFromHex(IUnit unit)
    {
        if (unit.Owner != Game.PhaseStepState?.ActivePlayer || unit.HasDeclaredWeaponAttack)
            return;

        if (Attacker is not null)
        {
            _rangeHighlighter.Clear();
            ResetUnitSelection();
        }

        SelectedUnit = unit;
    }

    /// <summary>
    /// Selects the enemy under the cursor when it is inside a weapon's reach.
    /// </summary>
    private bool TrySelectTargetFromHex(IUnit unit, Hex hex)
    {
        if (unit.Owner == Game.PhaseStepState?.ActivePlayer) return false;
        if (!_rangeHighlighter.IsHexInRange(hex.Coordinates)) return false;

        SelectedUnit = unit;
        return true;
    }

    public void HandleFacingSelection(HexDirection direction)
    {
        if (!this.CanHumanPlayerAct()) return;
        if (CurrentStep != WeaponsAttackStep.WeaponsConfiguration
            || Attacker == null
            || !_configuration.CanRotateTo(direction)) return;

        lock (_stateLock)
        {
            _configuration.ApplyTorsoRotation(Attacker, direction);

            // Return to action selection after rotation
            CurrentStep = WeaponsAttackStep.ActionSelection;
            _viewModel.NotifyStateChanged();
        }
    }

    public void HandleTorsoRotation(Guid unitId)
    {
        if (Attacker?.Id != unitId) return;
        _rangeHighlighter.Clear();
        HighlightWeaponRanges();
    }

    private void ResetUnitSelection()
    {
        lock (_stateLock)
        {
            SelectedTarget = null;
            Attacker?.WeaponAttackState.ClearAllWeaponTargets();
            Attacker = null;
            _rangeHighlighter.Clear();
            _presenter.Clear();
            CurrentStep = WeaponsAttackStep.SelectingUnit;
            _viewModel.NotifyStateChanged();
        }
    }

    private void CancelTorsoRotation()
    {
        if (CurrentStep != WeaponsAttackStep.WeaponsConfiguration) return;

        // Hide the direction selector
        _configuration.HideDirectionSelector();

        // Return to an action selection step
        CurrentStep = WeaponsAttackStep.ActionSelection;
        _viewModel.NotifyStateChanged();
    }

    private void CancelTargetSelection()
    {
        lock (_stateLock)
        {
            if (CurrentStep != WeaponsAttackStep.TargetSelection) return;

            // Clear target and weapon selections
            Attacker?.WeaponAttackState.ClearAllWeaponTargets();
            SelectedTarget = null;
            _viewModel.IsWeaponSelectionVisible = false;

            // Return to an action selection step
            CurrentStep = WeaponsAttackStep.ActionSelection;
            _viewModel.NotifyStateChanged();
        }
    }

    public IEnumerable<StateAction> GetAvailableActions()
    {
        if (Attacker is not { } attacker)
            return [];

        return WeaponsAttackActionBuilder.Build(
            CurrentStep,
            hasAttacker: true,
            attackerIsImmobile: attacker.IsImmobile,
            canFireWeapons: attacker.CanFireWeapons,
            hasSelectedWeapons: attacker.WeaponAttackState.SelectedWeapons.Any(),
            skipLabel: _viewModel.LocalizationService.GetString("Action_SkipAttack"),
            turnTorsoLabel: _viewModel.LocalizationService.GetString("Action_TurnTorso"),
            selectTargetLabel: _viewModel.LocalizationService.GetString("Action_SelectTarget"),
            declareLabel: _viewModel.LocalizationService.GetString("Action_DeclareAttack"),
            skip: ConfirmWeaponSelections,
            turnTorso: _configuration.GetTorsoRotationAction(attacker, () =>
            {
                CurrentStep = WeaponsAttackStep.WeaponsConfiguration;
                _viewModel.NotifyStateChanged();
            }),
            selectTarget: () =>
            {
                CurrentStep = WeaponsAttackStep.TargetSelection;
                _viewModel.NotifyStateChanged();
            });
    }

    private void HighlightWeaponRanges()
    {
        if (Attacker is not { } attacker) return;
        _rangeHighlighter.Highlight(attacker, Game);
    }

    private void ClearWeaponRangeHighlights() => _rangeHighlighter.Clear();

    /// <summary>
    /// Gets all weapons that can fire at a given hex coordinate
    /// </summary>
    public IReadOnlyList<Weapon> GetWeaponsInRange(HexCoordinates target) =>
        _rangeHighlighter.GetWeaponsInRange(target);

    public IUnit? Attacker
    {
        get;
        private set
        {
            field = value;
            _viewModel.NotifySelectedUnitChanged();
        }
    }

    public IUnit? SelectedTarget
    {
        get;
        private set
        {
            field = value;
            UpdateSelectedTargetViewModel();
            _viewModel.NotifySelectedUnitChanged();
        }
    }

    public IUnit? PrimaryTarget => Attacker?.WeaponAttackState.PrimaryTarget;

    public IEnumerable<WeaponSelectionViewModel> WeaponSelectionItems => _presenter.Items;

    private void HandleWeaponSelection(Weapon weapon, bool selected)
    {
        if (SelectedTarget == null || Attacker == null)
            return;

        if (!selected)
        {
            Attacker.WeaponAttackState.RemoveWeaponTarget(weapon, Attacker);
        }
        else
        {
            Attacker.WeaponAttackState.SetWeaponTarget(weapon, SelectedTarget, Attacker);
        }

        _presenter.Update(Attacker, SelectedTarget, _rangeHighlighter);
        UpdateSelectedTargetViewModel();
        _viewModel.NotifyStateChanged();
    }

    private void HandleAimedShotRequest(WeaponSelectionViewModel weaponVm) =>
        _presenter.ShowAimedShotSelector(weaponVm);

    private void HandleSetPrimaryTarget(IUnit target)
    {
        if (Attacker == null) return;

        Attacker.WeaponAttackState.SetPrimaryTarget(target);
        UpdateSelectedTargetViewModel();
        _presenter.Update(Attacker, SelectedTarget, _rangeHighlighter);
        _viewModel.NotifyStateChanged();
    }

    private void UpdateSelectedTargetViewModel() =>
        _presenter.UpdateSelectedTarget(Attacker, SelectedTarget, HandleSetPrimaryTarget);

    public void ConfirmWeaponSelections()
    {
        if (Attacker is not { } attacker)
            return;

        _submitter.Submit(Game, attacker, _presenter.GetAimedShotTarget);

        // Reset state after sending command
        ClearWeaponRangeHighlights();
        attacker.WeaponAttackState.ClearAllWeaponTargets();
        SelectedTarget = null;
        Attacker = null;
        _viewModel.IsWeaponSelectionVisible = false;

        // Clear the weapon view models
        _presenter.Clear();

        CurrentStep = WeaponsAttackStep.SelectingUnit;
        _viewModel.NotifyStateChanged();
    }
}

