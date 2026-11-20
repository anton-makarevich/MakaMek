using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// The physical attack declaration state: pick the attacker, pick the target, then declare
/// through one dialog that lists every supported attack type. It mirrors
/// <see cref="WeaponsAttackState"/>, but every eligibility decision comes from
/// <see cref="IPhysicalAttackEligibility"/> so the UI cannot disagree with the server about what
/// is legal. Availability and modifiers depend on the target, so the attack type is chosen only
/// after the target is known.
/// </summary>
/// <remarks>
/// The presentation layer writes this state on the UI thread, but <see cref="UnitControl"/> reads
/// <see cref="GetAvailableActions"/> from its 30 fps polling observable before hopping to the UI
/// thread. Every read and write of the mutable state therefore happens under
/// <see cref="_stateLock"/>: the public properties lock in their accessors and the private helpers
/// that need a coherent view of several fields lock for the duration of that view.
/// </remarks>
public class PhysicalAttackState : IUiState, ICombatSelectionRoles, IPhysicalAttackDeclarationState
{
    private static readonly IReadOnlyList<PhysicalAttackOptionViewModel> NoOptions = [];

    private readonly BattleMapViewModel _viewModel;
    private readonly Lock _stateLock = new();
    private readonly PhysicalAttackOptionBuilder _optionBuilder;
    private readonly PhysicalAttackTargeting _targeting;

    private IReadOnlyList<PhysicalAttackOptionViewModel> _options = NoOptions;
    private PhysicalAttackStep _currentStep = PhysicalAttackStep.SelectingUnit;
    private PhysicalAttackType? _selectedAttackType;
    private IUnit? _attacker;
    private IUnit? _selectedTarget;

    public IClientGame Game { get; }

    public PhysicalAttackStep CurrentStep
    {
        get
        {
            lock (_stateLock) return _currentStep;
        }
        private set
        {
            lock (_stateLock) _currentStep = value;
        }
    }

    public PhysicalAttackType? SelectedAttackType
    {
        get
        {
            lock (_stateLock) return _selectedAttackType;
        }
        private set
        {
            lock (_stateLock) _selectedAttackType = value;
        }
    }

    public IUnit? Attacker
    {
        get
        {
            lock (_stateLock) return _attacker;
        }
        private set
        {
            lock (_stateLock) _attacker = value;
        }
    }

    public IUnit? SelectedTarget
    {
        get
        {
            lock (_stateLock) return _selectedTarget;
        }
        private set
        {
            lock (_stateLock) _selectedTarget = value;
        }
    }

    /// <summary>
    /// One row per supported attack type against the selected target. Empty outside the
    /// declaration step.
    /// </summary>
    public IReadOnlyList<PhysicalAttackOptionViewModel> Options
    {
        get
        {
            lock (_stateLock) return _options;
        }
        private set
        {
            lock (_stateLock) _options = value;
        }
    }

    /// <inheritdoc />
    public bool IsDeclarationOpen => CurrentStep == PhysicalAttackStep.Declaration;

    /// <inheritdoc />
    public IUnit? DeclarationAttacker => Attacker;

    /// <inheritdoc />
    public IUnit? DeclarationTarget => SelectedTarget;

    /// <inheritdoc />
    public IReadOnlyList<PhysicalAttackOptionViewModel> DeclarationOptions => Options;

    internal PhysicalAttackState(
        BattleMapViewModel viewModel,
        PhysicalAttackOptionBuilder optionBuilder,
        PhysicalAttackTargeting targeting)
    {
        Game = viewModel.Game! ?? throw new InvalidOperationException("Game is not client game");
        _viewModel = viewModel;
        _optionBuilder = optionBuilder;
        _targeting = targeting;
        if (Game.PhaseStepState?.ActivePlayer == null)
        {
            throw new InvalidOperationException("Active player is null");
        }
    }

    public string ActionLabel
    {
        get
        {
            lock (_stateLock)
            {
                return _currentStep switch
                {
                    PhysicalAttackStep.SelectingUnit => GetString("Action_SelectUnitToAttack"),
                    PhysicalAttackStep.TargetSelection => GetString("Action_SelectTarget"),
                    PhysicalAttackStep.Declaration => GetString("Action_SelectPhysicalAttack"),
                    _ => string.Empty
                };
            }
        }
    }

    public bool IsActionRequired => this.CanHumanPlayerAct();

    public bool CanExecutePlayerAction
    {
        get
        {
            lock (_stateLock)
            {
                return _currentStep is PhysicalAttackStep.TargetSelection or PhysicalAttackStep.Declaration
                       && !_viewModel.AreActionsMenuOffMap;
            }
        }
    }

    public string PlayerActionLabel
    {
        get
        {
            if (!IsActionRequired) return string.Empty;

            lock (_stateLock)
            {
                return _currentStep switch
                {
                    PhysicalAttackStep.TargetSelection => GetString("Action_SkipAttack"),
                    PhysicalAttackStep.Declaration => _selectedAttackType != null
                        ? GetString("Action_DeclareAttack")
                        : GetString("Action_SkipAttack"),
                    _ => string.Empty
                };
            }
        }
    }

    public void ExecutePlayerAction()
    {
        if (!this.CanHumanPlayerAct()) return;

        lock (_stateLock)
        {
            switch (_currentStep)
            {
                case PhysicalAttackStep.TargetSelection:
                    Skip();
                    break;
                case PhysicalAttackStep.Declaration:
                    ConfirmDeclaration();
                    break;
            }
        }
    }

    public IUnit? SelectedUnit
    {
        get
        {
            lock (_stateLock)
                return _currentStep == PhysicalAttackStep.Declaration ? _selectedTarget : _attacker;
        }
        private set
        {
            lock (_stateLock)
            {
                var outcome = DecideSelection(value);
                switch (outcome)
                {
                    case PhysicalAttackSelectionOutcome.SelectAttacker:
                        SetAttacker(value!);
                        return;
                    case PhysicalAttackSelectionOutcome.OpenDeclaration:
                        OpenDeclaration(value!);
                        return;
                    case PhysicalAttackSelectionOutcome.CloseDeclaration:
                        CloseDeclaration();
                        return;
                    default:
                        return;
                }
            }
        }
    }

    /// <summary>
    /// Asks the selection policy what the request means. Called under <see cref="_stateLock"/> so the
    /// candidate checks see the same state the outcome is applied to.
    /// </summary>
    private PhysicalAttackSelectionOutcome DecideSelection(IUnit? candidate) =>
        _currentStep.DecidePhysicalAttackSelection(
            candidate,
            this.CanHumanPlayerAct(),
            isFriendly: candidate is not null && candidate.IsFriendly(Game),
            canActAsAttacker: () => candidate is not null && candidate.CanActAsPhysicalAttacker(Game, _viewModel.Units),
            isReachable: candidate is not null && _targeting.IsReachable(candidate),
            isCurrentTarget: ReferenceEquals(candidate, _selectedTarget));

    public void HandleUnitSelectionFromList(IUnit? unit)
    {
        SelectedUnit = unit;
    }

    public void HandleHexSelection(Hex hex)
    {
        if (!this.CanHumanPlayerAct()) return;

        var unit = PhysicalAttackTargeting.FindUnitOnHex(_viewModel.Units, hex);

        switch (CurrentStep)
        {
            case PhysicalAttackStep.SelectingUnit:
                if (unit != null) SelectedUnit = unit;
                return;

            case PhysicalAttackStep.TargetSelection:
                // Friendly units switch the attacker; reachable enemies open the dialog; any other
                // hex is ignored so the attacker and its highlights stay.
                if (unit != null) SelectedUnit = unit;
                return;

            case PhysicalAttackStep.Declaration:
                // Another reachable enemy rebuilds the rows; anything else closes the dialog and
                // returns to target selection without losing the attacker.
                if (unit != null && !unit.IsFriendly(Game) && _targeting.IsReachable(unit))
                    SelectedUnit = unit;
                else if (unit == null || !ReferenceEquals(unit, _selectedTarget))
                    CloseDeclaration();
                return;
        }
    }

    public void HandleFacingSelection(HexDirection direction)
    {
        // A physical attack never needs a facing decision.
    }

    /// <summary>
    /// Only the active player's Mechs that still have an attack option can be picked as attackers.
    /// </summary>
    public bool CanSelectUnit(IUnit? unit)
    {
        if (unit == null) return true;

        lock (_stateLock)
        {
            return _currentStep is PhysicalAttackStep.SelectingUnit or PhysicalAttackStep.TargetSelection
                   && unit.CanActAsPhysicalAttacker(Game, _viewModel.Units);
        }
    }

    public IEnumerable<StateAction> GetAvailableActions()
    {
        lock (_stateLock)
        {
            return PhysicalAttackActionBuilder.Build(
                _currentStep,
                _attacker != null,
                _selectedAttackType != null,
                GetString("Action_SkipAttack"),
                GetString("Action_DeclareAttack"),
                Skip,
                ConfirmDeclaration);
        }
    }

    /// <summary>
    /// Closes the declaration dialog, drops the pending target and rows, and returns to target
    /// selection with the attacker and its reachable-target highlights intact.
    /// </summary>
    public void CloseDeclaration()
    {
        lock (_stateLock)
        {
            if (_currentStep != PhysicalAttackStep.Declaration) return;

            SelectedTarget = null;
            Options = NoOptions;
            SelectedAttackType = null;
            CurrentStep = PhysicalAttackStep.TargetSelection;
            _targeting.Rebuild(_attacker, Game, _viewModel.Units);
            _viewModel.ClearHighlights();
            _targeting.Highlight(_attacker, Game, _viewModel, GetString);
            _viewModel.NotifyStateChanged();
        }
    }

    private void OpenDeclaration(IUnit target)
    {
        lock (_stateLock)
        {
            SelectedTarget = target;
            Options = BuildOptions(target);
            SelectedAttackType = null;
            CurrentStep = PhysicalAttackStep.Declaration;
            _viewModel.NotifyStateChanged();
        }
    }

    /// <summary>
    /// Builds the declaration rows for the selected target through the injected builder.
    /// </summary>
    private IReadOnlyList<PhysicalAttackOptionViewModel> BuildOptions(IUnit target)
    {
        IUnit? attacker;
        lock (_stateLock) attacker = _attacker;

        if (attacker == null) return NoOptions;

        return _optionBuilder.Build(
            attacker,
            target,
            Game.GetAllPlayerUnits(),
            OnOptionSelectionChanged);
    }

    /// <summary>
    /// Keeps exactly one row selected: selecting a row clears the limb checkboxes of the others,
    /// and a deselection callback for a row that is not the current selection does nothing.
    /// </summary>
    private void OnOptionSelectionChanged(PhysicalAttackOptionViewModel source, bool isSelected)
    {
        lock (_stateLock)
        {
            if (isSelected)
            {
                foreach (var other in _options.Where(option => !ReferenceEquals(option, source)))
                    other.ClearSelection();

                SelectedAttackType = source.AttackType;
            }
            else if (_selectedAttackType == source.AttackType)
            {
                SelectedAttackType = null;
            }
        }

        _viewModel.NotifyStateChanged();
    }

    private void SetAttacker(IUnit value)
    {
        if (_attacker != null && !ReferenceEquals(_attacker, value))
            ResetAttackerDependentState();

        Attacker = value;
        _targeting.Rebuild(Attacker, Game, _viewModel.Units);
        _viewModel.ClearHighlights();
        _targeting.Highlight(Attacker, Game, _viewModel, GetString);
        CurrentStep = PhysicalAttackStep.TargetSelection;
        _viewModel.NotifyStateChanged();
    }

    private void ResetAttackerDependentState()
    {
        SelectedTarget = null;
        Options = NoOptions;
        SelectedAttackType = null;
        lock (_stateLock) _targeting.Reset();
        _viewModel.ClearHighlights();
    }

    private void ConfirmDeclaration()
    {
        PhysicalAttackType? selectedType;
        lock (_stateLock) selectedType = _selectedAttackType;

        if (selectedType is null)
        {
            Skip();
            return;
        }

        DeclareSelected();
    }

    /// <summary>
    /// Revalidates the pending selection before submitting: a stale row, a lost permission or an
    /// eligibility change after the dialog opened must not publish a command.
    /// </summary>
    private void DeclareSelected()
    {
        if (!this.CanHumanPlayerAct()) return;

        IUnit? attacker;
        IUnit? target;
        PhysicalAttackType? attackType;
        PhysicalAttackOptionViewModel? option;
        lock (_stateLock)
        {
            attacker = _attacker;
            target = _selectedTarget;
            attackType = _selectedAttackType;
            option = _options.FirstOrDefault(candidate =>
                candidate.IsSelected && candidate.AttackType == _selectedAttackType);
        }

        if (attacker == null || target == null || attackType is not { } type
            || option?.SelectedVariant?.Limbs is not { Count: > 0 } limbs)
            return;

        if (!Game.TryDeclarePhysicalAttack(attacker, target, type, limbs, Game.GetAllPlayerUnits()))
            return;

        ReturnToAttackerSelection();
    }

    private void Skip()
    {
        if (!this.CanHumanPlayerAct()) return;

        IUnit? attacker;
        lock (_stateLock) attacker = _attacker;
        if (attacker == null) return;

        Game.SubmitPhysicalAttack(attacker, null);
        ReturnToAttackerSelection();
    }

    private void ReturnToAttackerSelection()
    {
        Attacker = null;
        ResetAttackerDependentState();
        CurrentStep = PhysicalAttackStep.SelectingUnit;
        _viewModel.NotifyStateChanged();
    }
    private string GetString(string key) => _viewModel.LocalizationService.GetString(key);
}






