using Sanet.MakaMek.Core.Data.Game.Commands.Client.Builders;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.UiStates;

public class MovementState : IUiState, IPhysicalAttackDeclarationState
{
    internal static readonly IReadOnlyList<PhysicalAttackOptionViewModel> NoAttackOptions = [];

    internal readonly BattleMapViewModel _viewModel;
    internal readonly MoveUnitCommandBuilder _builder;
    internal IUnit? _selectedUnit;
    internal ReachableArea? _reachabilityData;
    internal readonly IReadOnlySet<HexCoordinates> _prohibitedHexes;
    internal readonly IReadOnlySet<HexCoordinates> _friendlyUnitsCoordinates;
    internal int _movementPoints;
    internal Dictionary<HexDirection, MovementPath> _possibleDirections = [];
    internal readonly Lock _stateLock = new();
    internal bool _isPostStandupMovement;
    internal MovementPath? _selectedPath;
    internal Guid? _deferredMovementUnitId;

    /// <summary>
    /// Legal movement-attack targets for the current path state, each with its cheapest validated
    /// path. Recomputed whenever reachability is recomputed; replaced as a whole, never mutated.
    /// </summary>
    internal IReadOnlyDictionary<IUnit, MovementAttackCandidate> _movementAttackCandidates =
        new Dictionary<IUnit, MovementAttackCandidate>();

    internal IUnit? _pendingAttackTarget;
    internal MovementPath? _pendingAttackPath;
    internal PhysicalAttackDeclarationData? _pendingAttackDeclaration;
    internal IReadOnlyList<PhysicalAttackOptionViewModel> _pendingAttackOptions = NoAttackOptions;

    internal IMovementStep _step;

    internal void TransitionTo(IMovementStep step)
    {
        _step = step;
        _viewModel.NotifyStateChanged();
    }

    public IClientGame? Game => _viewModel.Game;

    public bool CanSelectUnit(IUnit? unit) => this.ResolveCanSelectUnit(unit);

    public MovementState(BattleMapViewModel viewModel)
    {
        _viewModel = viewModel;
        if (_viewModel.Game == null)
        {
            throw new InvalidOperationException("Game is null");
        }

        if (_viewModel.Game.PhaseStepState?.ActivePlayer == null)
        {
            throw new InvalidOperationException("Active player is null");
        }
        _builder = new MoveUnitCommandBuilder(_viewModel.Game.Id, _viewModel.Game.PhaseStepState.Value.ActivePlayer.Id);

        // Get hexes with enemy units - these will be excluded from pathfinding
        _prohibitedHexes = _viewModel.Units
            .Where(u=>u.Owner?.Id != _viewModel.Game.PhaseStepState?.ActivePlayer.Id && u.Position!=null)
            .Select(u => u.Position!.Coordinates)
            .ToHashSet();

        _friendlyUnitsCoordinates = _viewModel.Units
            .Where(u=>u.Owner?.Id == _viewModel.Game.PhaseStepState?.ActivePlayer.Id && u.Position!=null)
            .Select(u => u.Position!.Coordinates)
            .ToHashSet();

        _step = this.CreateInitialStep();
    }

    public IUnit? SelectedUnit
    {
        get => _selectedUnit;
        internal set => this.ApplySelectedUnit(value);
    }

    public void HandleUnitSelectionFromList(IUnit? unit) => this.ApplyUnitSelectionFromList(unit);

    public void HandleMovementTypeSelection(MovementType movementType)
    {
        lock (_stateLock)
        {
            _step.HandleMovementTypeSelection(movementType);
        }
    }

    public void HandleHexSelection(Hex hex)
    {
        if (_pendingAttackTarget != null)
        {
            lock (_stateLock)
            {
                this.HandlePendingMovementAttackHexSelection(hex);
            }
            return;
        }

        if (this.HandleUnitSelectionFromHex(hex)) return;
        lock (_stateLock)
        {
            _step.HandleHexSelection(hex);
        }
    }

    public void HandleFacingSelection(HexDirection direction)
    {
        lock (_stateLock)
        {
            _step.HandleFacingSelection(direction);
        }
    }
    /// <inheritdoc />
    public bool IsDeclarationOpen => CurrentMovementStep == MovementStep.ConfirmMovementAttack;

    /// <inheritdoc />
    public IUnit? DeclarationAttacker => IsDeclarationOpen ? _selectedUnit : null;

    /// <inheritdoc />
    public IUnit? DeclarationTarget => _pendingAttackTarget;

    /// <inheritdoc />
    public IReadOnlyList<PhysicalAttackOptionViewModel> DeclarationOptions => _pendingAttackOptions;

    /// <inheritdoc />
    public void CloseDeclaration()
    {
        lock (_stateLock)
        {
            if (CurrentMovementStep != MovementStep.ConfirmMovementAttack) return;
            this.CancelPendingMovementAttack();
        }
    }

    public string ActionLabel => !IsActionRequired ? string.Empty : _step.ActionLabel;

    public bool IsActionRequired =>
        this.CanHumanPlayerAct() &&
        CurrentMovementStep != MovementStep.Completed;

    public bool CanExecutePlayerAction => CurrentMovementStep is MovementStep.ConfirmMovement
        or MovementStep.ConfirmMovementAttack;

    public string PlayerActionLabel => CurrentMovementStep switch
    {
        MovementStep.ConfirmMovement => _viewModel.LocalizationService.GetString("Action_MoveUnit"),
        MovementStep.ConfirmMovementAttack => this.GetMovementAttackActionLabel(),
        _ => string.Empty
    };

    public MovementStep CurrentMovementStep => _step.Step;

    public void ExecutePlayerAction()
    {
        lock (_stateLock)
        {
            _step.ExecutePlayerAction();
        }
    }
    // TODO that should be a part of UnitPresentationExtensions
    public IEnumerable<StateAction> GetAvailableActions()
    {
        lock (_stateLock)
        {
            if (CurrentMovementStep == MovementStep.ConfirmMovementAttack)
            {
                return
                [
                    new StateAction(this.GetMovementAttackActionLabel(), true, ExecutePlayerAction)
                ];
            }

            if (CurrentMovementStep != MovementStep.SelectingMovementType || _selectedUnit == null)
                return [];

            if (_selectedUnit is Mech { IsProne: true } mech
                && _viewModel.Game is not null)
            {
                // Post-fall: MovementTaken being non-null means the unit was already moving when it fell.
                // Lock the standup to the originally declared movement type
                return mech.MovementTaken != null
                    ? this.GetLockedProneActions(mech)
                    : this.GetStartOfPhaseProneActions(mech);
            }

            return this.GetNormalActions();
        }
    }
}
