using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands.Client.Builders;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Presentation.UiStates;

public class MovementState : IUiState
{
    private readonly BattleMapViewModel _viewModel;
    private readonly MoveUnitCommandBuilder _builder;
    private IUnit? _selectedUnit;
    private UnitReachabilityData? _reachabilityData;
    private readonly IReadOnlySet<HexCoordinates> _prohibitedHexes;
    private readonly IReadOnlySet<HexCoordinates> _friendlyUnitsCoordinates;
    private int _movementPoints;
    private Dictionary<HexDirection, MovementPath> _possibleDirections = [];
    private readonly Lock _stateLock = new();
    private bool _isPostStandupMovement;
    private MovementPath? _selectedPath;

    private IMovementStep _step;

    public IClientGame? Game => _viewModel.Game;

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

        _step = new SelectingUnitStep(this);
    }
    
    private void TransitionTo(IMovementStep step)
    {
        _step = step;
    }
    
    private void ClearHighlighting()
    {
        if (_reachabilityData != null)
            _viewModel.HighlightHexes(_reachabilityData.Value.AllReachableHexes, false);
        _reachabilityData = null;
    }

    private void HighlightReachableHexes()
    {
        if (_selectedPath?.MovementType == null) return;
        var movementType = _selectedPath.MovementType;

        ClearHighlighting();

        var remainingMp = GetRemainingMovementPoints();
        _movementPoints = remainingMp;

        var battleMap = _viewModel.Game?.BattleMap;
        if (_selectedUnit != null && battleMap != null)
        {
            var startPosition = _selectedPath.Destination;
            _reachabilityData = battleMap.GetReachableHexesForPosition(
                startPosition,
                remainingMp,
                _selectedUnit.CanMoveBackward(movementType),
                movementType,
                _prohibitedHexes,
                _friendlyUnitsCoordinates);

            _viewModel.HighlightHexes(_reachabilityData.Value.AllReachableHexes, true);
        }

        _viewModel.NotifyStateChanged();
    }

    private int GetRemainingMovementPoints()
    {
        if (_selectedPath == null || _selectedUnit == null) return 0;
        return Math.Max(0, _selectedUnit.GetMovementPoints(_selectedPath.MovementType) - _selectedPath.TotalCost);
    }

    public void HandleUnitSelection(IUnit? unit)
    {
        lock (_stateLock)
        {
            if (!this.CanHumanPlayerAct()) return;
            if (unit == null) return;
            if (unit.Status == UnitStatus.Destroyed) return;
            if (unit.HasMoved) return;

            _selectedUnit = unit;
            _builder.SetUnit(unit);
            _isPostStandupMovement = false; // Reset post-standup state when selecting a new unit
            TransitionTo(new SelectingMovementTypeStep(this));
            _viewModel.NotifyStateChanged();
        }
    }

    public void HandleMovementTypeSelection(MovementType movementType)
    {
        lock (_stateLock)
        {
            _step.HandleMovementTypeSelection(movementType);
        }
    }

    public void HandleHexSelection(Hex hex)
    {
        if (HandleUnitSelectionFromHex(hex)) return;
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

    private void ConfirmMovement()
    {
        if (CurrentMovementStep != MovementStep.ConfirmMovement) return;
        var direction = _viewModel.AvailableDirections?.FirstOrDefault();
        if (direction == null) return; 
        var path = _possibleDirections[direction.Value];
        if (_viewModel.MovementPath == null || _viewModel.MovementPath.Last().To != path.Destination)
        {
            if (_selectedUnit?.Position == null) return;
            path = MovementPath.CreateStandingStillPath(_selectedUnit.Position);
            _builder.SetMovementPath(path);
        }

        CompleteMovement();
    }

    private bool HandleUnitSelectionFromHex(Hex hex)
    {
        var unit = _viewModel.Units.FirstOrDefault(u => u.Position?.Coordinates == hex.Coordinates);
        if (unit == null 
            || unit == _selectedUnit
            || unit.Owner?.Id != _viewModel.Game?.PhaseStepState?.ActivePlayer.Id) return false;
        ResetUnitSelection();
        _viewModel.SelectedUnit=unit;
        return true;
    }

    private void ResetUnitSelection()
    {
        if (_isPostStandupMovement) return;
        lock (_stateLock)
        {
            if (_viewModel.SelectedUnit == null) return;
            _viewModel.SelectedUnit = null;
            _selectedUnit = null;
            _viewModel.HideMovementPath();
            _viewModel.HideDirectionSelector();
            ClearHighlighting();
            TransitionTo(new SelectingUnitStep(this));
            _viewModel.NotifyStateChanged();
        }
    }

    private void HandleTargetHexSelection(Hex hex)
    {
        if (_selectedUnit?.Position == null
            || _selectedPath?.MovementType == null
            || Game == null
            || _reachabilityData == null) return;

        // Reset selection if clicked outside reachable hexes during target hex selection
        // BUT NOT if the unit is in post-standup movement state (must complete declared movement)
        if (!_reachabilityData.Value.IsHexReachable(hex.Coordinates))
        {
            ResetUnitSelection();
            return;
        }
        
        // Use the extension method to find all possible paths to the target hex
        var startPosition = _selectedPath.Destination;
        _movementPoints = GetRemainingMovementPoints();
        _possibleDirections = Game.BattleMap?.GetPathsToHexWithAllFacings(
            startPosition,
            hex.Coordinates,
            _selectedPath.MovementType,
            _movementPoints,
            _reachabilityData.Value,
            _prohibitedHexes) ?? [];

        TransitionTo(new SelectingDirectionStep(this, hex.Coordinates));
    }
    
    private void CompleteMovement()
    {
        lock (_stateLock)
        {
            var command = _builder.Build();
            if (command != null && _viewModel.Game is { } clientGame)
            {
                _viewModel.HideMovementPath();
                _viewModel.HideDirectionSelector();
                clientGame.MoveUnit(command.Value);
            }

            _builder.Reset();
            ClearHighlighting();
            _selectedUnit = null;
            _isPostStandupMovement = false; // Reset post-standup state when movement is completed
            TransitionTo(new CompletedStep(this));
            _viewModel.NotifyStateChanged();
        }
    }

    public string ActionLabel => !IsActionRequired ? string.Empty : _step.ActionLabel;

    public bool IsActionRequired =>
        this.CanHumanPlayerAct() &&
        CurrentMovementStep != MovementStep.Completed;
    
    public bool CanExecutePlayerAction => CurrentMovementStep == MovementStep.ConfirmMovement;
    
    public string PlayerActionLabel => CurrentMovementStep == MovementStep.ConfirmMovement ? 
        _viewModel.LocalizationService.GetString("Action_MoveUnit") : string.Empty;
    
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
            if (CurrentMovementStep != MovementStep.SelectingMovementType || _selectedUnit == null)
                return [];

            // Check if the unit is a Mech and is prone
            if (_selectedUnit is Mech { IsProne: true } mech
                && _viewModel.Game is not null)
            {
                var proneActions = new List<StateAction>
                {
                    // Add stay prone action (equivalent to standing still for prone mechs)
                    new(
                        _viewModel.LocalizationService.GetString("Action_StayProne"),
                        true,
                        () => HandleMovementTypeSelection(MovementType.StandingStill))
                };

                if (mech.IsImmobile) return proneActions;

                // Add standup action if possible
                if (mech.CanStandup())
                {
                    // Calculate piloting skill roll breakdown and success probability for standing up
                    var psrBreakdown = _viewModel.Game.PilotingSkillCalculator.GetPsrBreakdown(
                        mech, PilotingSkillRollType.StandupAttempt);

                    var successProbability =
                        Core.Utils.DiceUtils.Calculate2d6Probability(psrBreakdown.ModifiedPilotingSkill);

                    // Format the probability as percentage
                    var probabilityText = $" ({successProbability:0}%)";

                    // Check if this is a minimum movement situation
                    if (mech.IsMinimumMovement)
                    {
                        // Minimum movement case: single "Attempt Standup" button
                        proneActions.Add(new StateAction(
                            _viewModel.LocalizationService.GetString("Action_AttemptStandup") + probabilityText,
                            true,
                            () => AttemptStandup(MovementType.Walk)));
                    }
                    else
                    {
                        // Non-minimum movement case: separate Walk and Run actions
                        // Walk standup action
                        var walkActionText = string.Format(
                            _viewModel.LocalizationService.GetString("Action_MovementPoints"),
                            _viewModel.LocalizationService.GetString("MovementType_Walk"),
                            _selectedUnit.GetMovementPoints(MovementType.Walk)) + probabilityText;

                        proneActions.Add(new StateAction(
                            walkActionText,
                            true,
                            () => AttemptStandup(MovementType.Walk)));

                        // Run standup action (only if mech can run)
                        if (mech.CanRun)
                        {
                            var runActionText = string.Format(
                                _viewModel.LocalizationService.GetString("Action_MovementPoints"),
                                _viewModel.LocalizationService.GetString("MovementType_Run"),
                                _selectedUnit.GetMovementPoints(MovementType.Run)) + probabilityText;

                            proneActions.Add(new StateAction(
                                runActionText,
                                true,
                                () => AttemptStandup(MovementType.Run)));
                        }
                    }
                }

                // Add facing change action if possible
                if (mech.CanChangeFacingWhileProne())
                {
                    var availableMp = mech.GetMovementPoints(MovementType.Walk);
                    proneActions.Add(new StateAction(
                        string.Format(_viewModel.LocalizationService.GetString("Action_ChangeFacing"), availableMp),
                        true,
                        () => HandleProneFacingChange(mech)));
                }

                return proneActions;
            }

            var actions = new List<StateAction>
            {
                // Stand Still
                new(
                    _viewModel.LocalizationService.GetString("Action_StandStill"),
                    true,
                    () => HandleMovementTypeSelection(MovementType.StandingStill)),

            };
            if (_selectedUnit.IsImmobile) return actions;
            
            // Walk
            actions.Add(new(
                string.Format(_viewModel.LocalizationService.GetString("Action_MovementPoints"),
                    _viewModel.LocalizationService.GetString("MovementType_Walk"),
                    _selectedUnit.GetMovementPoints(MovementType.Walk)),
                true,
                () => HandleMovementTypeSelection(MovementType.Walk)));
            
            // Run
            if (_selectedUnit is Mech { CanRun: true })
            {
                actions.Add(new(
                    string.Format(_viewModel.LocalizationService.GetString("Action_MovementPoints"),
                        _viewModel.LocalizationService.GetString("MovementType_Run"),
                        _selectedUnit.GetMovementPoints(MovementType.Run)),
                    true,
                    () => HandleMovementTypeSelection(MovementType.Run)));
            }

            // Jump
            if (!(_selectedUnit is Mech { CanJump: true })) return actions;
            var jumpPoints = _selectedUnit.GetMovementPoints(MovementType.Jump);

            var jumpActionText = string.Format(_viewModel.LocalizationService.GetString("Action_MovementPoints"),
                _viewModel.LocalizationService.GetString("MovementType_Jump"),
                jumpPoints);

            // Check if PSR is required for jumping with damaged components and add probability
            if (_selectedUnit is Mech jumpMech && jumpMech.IsPsrForJumpRequired() && _viewModel.Game is not null)
            {
                var psrBreakdown = _viewModel.Game.PilotingSkillCalculator.GetPsrBreakdown(
                    jumpMech, PilotingSkillRollType.JumpWithDamage);

                var successProbability =
                    Core.Utils.DiceUtils.Calculate2d6Probability(psrBreakdown.ModifiedPilotingSkill);
                var probabilityText = $" ({successProbability:0}%)";
                jumpActionText += probabilityText;
            }

            actions.Add(new StateAction(
                jumpActionText,
                true,
                () => HandleMovementTypeSelection(MovementType.Jump)));

            return actions;
        }
    }

    // A method to handle standup attempts
    private void AttemptStandup(MovementType movementType)
    {
        lock (_stateLock)
        {
            if (_viewModel.Game?.PhaseStepState?.ActivePlayer == null) return;
            if (_selectedUnit?.Position == null) return;

            // Set the selected movement type for later use
            _selectedPath = new MovementPath([
                new PathSegment(_selectedUnit.Position, _selectedUnit.Position, 0)],
                movementType);
            // Ensure the builder has the movement type set
            _builder.SetMovementPath(_selectedPath);

            TransitionTo(new SelectingStandingUpDirectionStep(this));
            _viewModel.ShowDirectionSelector(_selectedUnit.Position.Coordinates, Enum.GetValues<HexDirection>());
            _viewModel.NotifyStateChanged();
        }
    }

    // New method to handle standup with direction selection
    private void CompleteStandupAttempt(HexDirection direction)
    {
        lock (_stateLock)
        {
            if (_viewModel.Game?.PhaseStepState?.ActivePlayer == null) return;
            if (_selectedUnit?.Position == null) return;
            if (_selectedPath?.MovementType == null) return;

            // Create a standup command with the selected direction
            var standupCommand = new TryStandupCommand
            {
                GameOriginId = _viewModel.Game.Id,
                UnitId = _selectedUnit.Id,
                PlayerId = _viewModel.Game.PhaseStepState.Value.ActivePlayer.Id,
                NewFacing = direction,
                MovementTypeAfterStandup = _selectedPath.MovementType
            };

            // Publish the command
            _viewModel.Game.TryStandupUnit(standupCommand);

            // Reset the movement state
            _viewModel.HideDirectionSelector();
            _viewModel.NotifyStateChanged();
        }
    }

    // Method to resume movement after successful standup
    public void ResumeMovementAfterStandup()
    {
        lock (_stateLock)
        {
            // Check if the unit is no longer prone (standup was successful)
            if (_selectedPath?.MovementType == null ||
                _selectedUnit is not Mech { IsProne: false, Position: not null } mech) return;

            if (mech.GetMovementPoints(_selectedPath.MovementType) < 1)
            {
                // No more movement possible, just confirm
                CompleteMovement();
                return;
            }
            
            _isPostStandupMovement = true; // Mark that this unit is in post-standup movement state
            _selectedPath = new MovementPath([
                new PathSegment(mech.Position, mech.Position, 0)],
                _selectedPath.MovementType);
            _builder.SetMovementPath(_selectedPath);
            HighlightReachableHexes();
            TransitionTo(new SelectingTargetHexStep(this));
            _viewModel.NotifyStateChanged();
        }
    }

    public void ResumeMovementAfterFall()
    {
        lock (_stateLock)
        {
            if (_selectedUnit is not Mech { IsProne: true, Position: not null } mech || _selectedPath == null)
            {
                var exception = new InvalidOperationException("Unit is not prone after fall or no movement path");
                Game?.Logger.LogError(exception, "Unit is not prone after fall or no movement path");
                throw exception;
            }

            if (!mech.CanStandup())
            {
                _builder.SetMovementPath(_selectedPath);
                CompleteMovement();
                return;
            }
            
            TransitionTo(new SelectingMovementTypeStep(this));
            _viewModel.NotifyStateChanged();
        }
    }

    // New method to handle prone facing change
    private void HandleProneFacingChange(Mech mech)
    {
        if (_viewModel.Game?.PhaseStepState?.ActivePlayer == null) return;
        if (mech.Position == null || !mech.IsProne) return;

        _movementPoints = mech.GetMovementPoints(MovementType.Walk);

        // Calculate maximum rotation steps based on available movement points
        var maxRotateSteps = Math.Min(3, _movementPoints);

        // Reset possible directions
        _possibleDirections = [];

        var currentFacing = mech.Facing;
        if (currentFacing == null) return;
        
        _selectedPath = new MovementPath([
            new PathSegment(mech.Position, mech.Position, 0)],
            MovementType.Walk);
        _builder.SetMovementPath(_selectedPath);

        void AddToPossibleDirections(HexDirection direction, int cost)
        {
            var pathSegments = new MovementPath([
                new PathSegment(mech.Position, mech.Position with { Facing = direction }, cost)
            ], MovementType.Walk);
            _possibleDirections[direction] = pathSegments;
        }

        // Generate possible directions by rotating from the current facing
        for (var steps = 1; steps <= maxRotateSteps; steps++)
        {
            var rotatedDirectionCw = currentFacing.Value.Rotate(steps);
            AddToPossibleDirections(rotatedDirectionCw, steps);
        
            if (steps == 3) break; // We don't want to duplicate an opposite direction
        
            var rotatedDirectionCcw = currentFacing.Value.Rotate(-steps);
            AddToPossibleDirections(rotatedDirectionCcw, steps);
        }

        TransitionTo(new SelectingDirectionStep(this, mech.Position.Coordinates));
    }

    private interface IMovementStep
    {
        MovementStep Step { get; }
        string ActionLabel { get; }
        void HandleMovementTypeSelection(MovementType movementType);
        void HandleHexSelection(Hex hex);
        void HandleFacingSelection(HexDirection direction);
        void ExecutePlayerAction();
    }

    private abstract class MovementStepBase : IMovementStep
    {
        protected readonly MovementState State;

        protected MovementStepBase(MovementState state)
        {
            State = state;
        }

        public abstract MovementStep Step { get; }
        public abstract string ActionLabel { get; }
        public virtual void HandleMovementTypeSelection(MovementType movementType) { }
        public virtual void HandleHexSelection(Hex hex) { }
        public virtual void HandleFacingSelection(HexDirection direction) { }
        public virtual void ExecutePlayerAction() { }
    }

    private sealed class SelectingUnitStep : MovementStepBase
    {
        public SelectingUnitStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.SelectingUnit;
        public override string ActionLabel => State._viewModel.LocalizationService.GetString("Action_SelectUnitToMove");
    }

    private sealed class SelectingMovementTypeStep : MovementStepBase
    {
        public SelectingMovementTypeStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.SelectingMovementType;
        public override string ActionLabel => State._viewModel.LocalizationService.GetString("Action_SelectMovementType");

        public override void HandleMovementTypeSelection(MovementType movementType)
        {
            if (!State.CanHumanPlayerAct()) return;
            if (State._selectedUnit?.Position == null) return;
            if (State.CurrentMovementStep != MovementStep.SelectingMovementType) return;
            
            if (movementType == MovementType.StandingStill)
            {
                // For standing still, we create an empty movement path
                var path = MovementPath.CreateStandingStillPath(State._selectedUnit.Position);
                State._builder.SetMovementPath(path);
                State.CompleteMovement();
                return;
            }

            State._selectedPath = new MovementPath([
                    new PathSegment(State._selectedUnit.Position, State._selectedUnit.Position, 0)],
                movementType);
            State.HighlightReachableHexes();
            State.TransitionTo(new SelectingTargetHexStep(State));
        }
    }

    private sealed class SelectingTargetHexStep : MovementStepBase
    {
        public SelectingTargetHexStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.SelectingTargetHex;
        public override string ActionLabel => State._viewModel.LocalizationService.GetString("Action_SelectTargetHex");

        public override void HandleHexSelection(Hex hex)
        {
            State.HandleTargetHexSelection(hex);
        }
    }

    private sealed class SelectingDirectionStep : MovementStepBase
    {
        public SelectingDirectionStep(MovementState state, HexCoordinates targetHex) : base(state)
        {
            if (State._possibleDirections.Count == 0) return;
            State._viewModel.ShowDirectionSelector(targetHex, State._possibleDirections.Keys);
            State._viewModel.NotifyStateChanged();
        }
        public override MovementStep Step => MovementStep.SelectingDirection;
        public override string ActionLabel => State._viewModel.LocalizationService.GetString("Action_SelectFacingDirection");

        public override void HandleHexSelection(Hex hex)
        {
            State.HandleTargetHexSelection(hex);
        }

        public override void HandleFacingSelection(HexDirection direction)
        {
            if (State.CurrentMovementStep != MovementStep.SelectingDirection) return;
            if (!State._possibleDirections.TryGetValue(direction, out var path)) return;

            State._selectedPath = State._selectedPath?.Append(path) ?? path;
            State._builder.SetMovementPath(State._selectedPath);

            State._viewModel.ShowDirectionSelector(path.Destination.Coordinates, [direction]);
            State._viewModel.ShowMovementPath(State._selectedPath);

            var movementType = State._selectedPath.MovementType;
            var remainingMp = State.GetRemainingMovementPoints();
            State.TransitionTo(new ConfirmMovementStep(State));
            if (movementType is MovementType.Walk or MovementType.Run && remainingMp > 0)
            {
                State.HighlightReachableHexes();
            }
            State._viewModel.NotifyStateChanged();
        }
    }

    private sealed class ConfirmMovementStep : MovementStepBase
    {
        public ConfirmMovementStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.ConfirmMovement;
        public override string ActionLabel => GetActionLabel();

        private string GetActionLabel()
        {
            var remainingMp = State.GetRemainingMovementPoints();
            var movementType = State._selectedPath?.MovementType;
            
            // Check if we can continue movement (walk/run with remaining MPs)
            if (movementType is MovementType.Walk or MovementType.Run && remainingMp > 0)
            {
                return State._viewModel.LocalizationService.GetString("Action_ConfirmOrSelectNextHex");
            }
            
            // No more movement possible, just confirm
            return State._viewModel.LocalizationService.GetString("Action_ConfirmMovement");
        }

        public override void HandleHexSelection(Hex hex)
        {
            // Reset selection if clicked outside reachable hexes during the confirmation step
            // This matches the behavior in HandleTargetHexSelection and SelectingDirectionStep
            if (State._reachabilityData != null && !State._reachabilityData.Value.IsHexReachable(hex.Coordinates))
            {
                State.ResetUnitSelection();
                return;
            }

            if (State._selectedPath?.MovementType is not (MovementType.Walk or MovementType.Run)) return;
            if (State.GetRemainingMovementPoints() <= 0) return;
            State.HandleTargetHexSelection(hex);
        }

        public override void HandleFacingSelection(HexDirection direction)
        {
            State.ConfirmMovement();
        }

        public override void ExecutePlayerAction()
        {
            State.ConfirmMovement();
        }
    }

    private sealed class SelectingStandingUpDirectionStep : MovementStepBase
    {
        public SelectingStandingUpDirectionStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.SelectingStandingUpDirection;
        public override string ActionLabel => State._viewModel.LocalizationService.GetString("Action_SelectFacingDirection");

        public override void HandleFacingSelection(HexDirection direction)
        {
            State.CompleteStandupAttempt(direction);
        }
    }

    private sealed class CompletedStep : MovementStepBase
    {
        public CompletedStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.Completed;
        public override string ActionLabel => string.Empty;
    }
}
