using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Highlights;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Presents the physical-attack phase and its safe pass action while punch/kick resolution is added.
/// </summary>
public sealed class PhysicalAttackState : IUiState
{
    private readonly BattleMapViewModel _viewModel;
    private readonly HashSet<Guid> _submittedUnits = [];
    private readonly PhysicalAttackValidator _validator = new();
    private HashSet<HexCoordinates> _targetCoordinates = [];
    private IUnit? _selectedUnit;
    private IUnit? _selectedTarget;
    private PhysicalAttackType? _selectedAttackType;

    /// <summary>Initializes the state for the current game phase.</summary>
    public PhysicalAttackState(BattleMapViewModel viewModel)
    {
        _viewModel = viewModel;
        Game = viewModel.Game ?? throw new InvalidOperationException("Game is null");
        if (Game.PhaseStepState?.ActivePlayer == null)
            throw new InvalidOperationException("Active player is null");
    }

    /// <inheritdoc />
    public IClientGame Game { get; }

    /// <inheritdoc />
    public IUnit? SelectedUnit
    {
        get => _selectedUnit;
        private set
        {
            if (_selectedUnit == value) return;
            _selectedUnit = value;
            _viewModel.NotifySelectedUnitChanged();
            _viewModel.NotifyStateChanged();
        }
    }

    /// <inheritdoc />
    public string ActionLabel => SelectedUnit == null
        ? _viewModel.LocalizationService.GetString("Action_SelectUnitForPhysicalAttack")
        : _selectedTarget == null
            ? _viewModel.LocalizationService.GetString("Action_SelectPhysicalAttackTarget")
            : _viewModel.LocalizationService.GetString("Action_SelectPhysicalAttack");

    /// <inheritdoc />
    public bool IsActionRequired => this.IsActiveHumanPlayer() && Game.CanActivePlayerAct;

    /// <inheritdoc />
    public bool CanExecutePlayerAction => SelectedUnit != null && this.CanHumanPlayerAct();

    /// <inheritdoc />
    public string PlayerActionLabel => _selectedAttackType is { } attackType
        ? attackType.ToString()
        : _viewModel.LocalizationService.GetString("Action_PassPhysicalAttack");

    /// <inheritdoc />
    public bool CanSelectUnit(IUnit? unit) => unit is not null
        && unit.Owner?.Id == Game.PhaseStepState?.ActivePlayer.Id
        && !_submittedUnits.Contains(unit.Id)
        && !unit.IsDestroyed;

    /// <inheritdoc />
    public void HandleUnitSelectionFromList(IUnit? unit)
    {
        if (!this.CanHumanPlayerAct() || !CanSelectUnit(unit)) return;
        SelectedUnit = unit;
        HighlightTargets();
    }

    /// <inheritdoc />
    public void HandleHexSelection(Hex hex)
    {
        if (!this.CanHumanPlayerAct()) return;
        var unit = _viewModel.Units.FirstOrDefault(candidate => candidate.Position?.Coordinates == hex.Coordinates);
        if (SelectedUnit == null)
        {
            HandleUnitSelectionFromList(unit);
            return;
        }

        if (unit == null || unit.Owner?.Id == Game.PhaseStepState?.ActivePlayer.Id) return;
        if (_validator.Validate(SelectedUnit, unit, PhysicalAttackType.Punch).IsValid)
        {
            _selectedTarget = unit;
            _viewModel.NotifyStateChanged();
        }
    }

    /// <inheritdoc />
    public void HandleFacingSelection(HexDirection direction)
    {
        // Physical attacks do not use a facing selector in the initial slice.
    }

    /// <inheritdoc />
    public IEnumerable<StateAction> GetAvailableActions()
    {
        if (!CanExecutePlayerAction) return [];

        var actions = new List<StateAction>
        {
            new(
                _viewModel.LocalizationService.GetString("Action_PassPhysicalAttack"),
                true,
                () => SendPhysicalAttack(null))
        };

        if (_selectedTarget != null)
        {
            actions.Insert(0, new StateAction(
                _viewModel.LocalizationService.GetString("Action_Punch"),
                true,
                () => SendPhysicalAttack(PhysicalAttackType.Punch)));
            actions.Insert(1, new StateAction(
                _viewModel.LocalizationService.GetString("Action_Kick"),
                true,
                () => SendPhysicalAttack(PhysicalAttackType.Kick)));
        }

        return actions;
    }

    /// <inheritdoc />
    public void ExecutePlayerAction()
    {
        if (!CanExecutePlayerAction || SelectedUnit == null) return;
        SendPhysicalAttack(_selectedAttackType);
    }

    private void SendPhysicalAttack(PhysicalAttackType? attackType)
    {
        if (!CanExecutePlayerAction || SelectedUnit == null) return;

        var unit = SelectedUnit;
        _submittedUnits.Add(unit.Id);
        var target = _selectedTarget;
        _viewModel.RemoveHighlight<AttackReachableHighlight>(_targetCoordinates);
        _targetCoordinates = [];
        _selectedTarget = null;
        _selectedAttackType = null;
        SelectedUnit = null;
        if (attackType is { } selectedAttack && target != null)
        {
            _ = Game.DeclarePhysicalAttack(new PhysicalAttackCommand
            {
                GameOriginId = Game.Id,
                PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
                UnitId = unit.Id,
                TargetUnitId = target.Id,
                AttackType = selectedAttack
            });
            return;
        }

        _ = Game.PassPhysicalAttack(new PassPhysicalAttackCommand
        {
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = unit.Id
        });
    }

    private void HighlightTargets()
    {
        if (SelectedUnit?.Position == null) return;

        _targetCoordinates = _viewModel.Units
            .Where(unit => unit.Owner?.Id != Game.PhaseStepState?.ActivePlayer.Id)
            .Where(unit => _validator.Validate(SelectedUnit, unit, PhysicalAttackType.Punch).IsValid)
            .Where(unit => unit.Position != null)
            .Select(unit => unit.Position!.Coordinates)
            .ToHashSet();

        if (_targetCoordinates.Count > 0)
            _viewModel.HighlightCoordinates(
                _targetCoordinates,
                new AttackReachableHighlight([], AttackRangeBand.Short, "Physical attack target"));
    }
}
