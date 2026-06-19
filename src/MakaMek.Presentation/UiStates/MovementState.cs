using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands.Client.Builders;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Models.Highlights;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.UiStates;

public class MovementState : IUiState
{
    private readonly BattleMapViewModel _viewModel;
    private readonly MoveUnitCommandBuilder _builder;
    private IUnit? _selectedUnit;
    private ReachableArea? _reachabilityData;
    private readonly IReadOnlySet<HexCoordinates> _prohibitedHexes;
    private readonly IReadOnlySet<HexCoordinates> _friendlyUnitsCoordinates;
    private int _movementPoints;
    private Dictionary<HexDirection, MovementPath> _possibleDirections = [];
    private readonly Lock _stateLock = new();
    private bool _isPostStandupMovement;
    private MovementPath? _selectedPath;
    private Guid? _deferredMovementUnitId;

    private IMovementStep _step;

    public IClientGame? Game => _viewModel.Game;

    public bool CanSelectUnit(IUnit? unit)
    {
        if (_deferredMovementUnitId is not { } lockedId) return true;
        if (unit == null) return true;
        return unit.Id == lockedId;
    }

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
        _viewModel.NotifyStateChanged();
    }
    
    private void ClearHighlighting()
    {
        if (_reachabilityData != null)
            _viewModel.RemoveHighlight<MovementReachableHighlight>(_reachabilityData.Value.AllReachableCoordinates);
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
                _friendlyUnitsCoordinates,
                _selectedUnit.Height,
                _selectedUnit.MaxLevelChangeForward,
                _selectedUnit.MaxLevelChangeBackward);

            _viewModel.AddHighlight(_reachabilityData.Value.AllReachableCoordinates, new MovementReachableHighlight(movementType));
        }

        _viewModel.NotifyStateChanged();
    }

    private int GetRemainingMovementPoints()
    {
        if (_selectedPath == null || _selectedUnit == null) return 0;
        return Math.Max(0, _selectedUnit.GetMovementPoints(_selectedPath.MovementType) - _selectedPath.TotalCost);
    }

    public IUnit? SelectedUnit
    {
        get => _selectedUnit;
        private set
        {
            lock (_stateLock)
            {
                if (!this.CanHumanPlayerAct()) return;
                if (value == null)
                {
                    ResetUnitSelection();
                    return;
                }
                
                if (!CanSelectUnit(value)) return;
                
                if (value.Status == UnitStatus.Destroyed) return;
                if (value.HasMoved && value.Id != _deferredMovementUnitId) return;

                _selectedUnit = value;
                _builder.SetUnit(value);
                _isPostStandupMovement = false;
                TransitionTo(new SelectingMovementTypeStep(this));
                _viewModel.NotifySelectedUnitChanged();
            }
        }
    }

    public void HandleUnitSelectionFromList(IUnit? unit)
    {
        if (unit != null && _selectedUnit != null && _selectedUnit != unit)
        {
            ResetUnitSelection();
        }

        SelectedUnit = unit;
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
            path = MovementPath.CreateSingleSegmentPath(_selectedUnit.Position);
            _builder.SetMovementPath(path);
        }

        CompleteMovement();
    }

    private bool HandleUnitSelectionFromHex(Hex hex)
    {
        var unit = _viewModel.Units.FirstOrDefault(u => u.Position?.Coordinates == hex.Coordinates);
        if (unit == null) return false;
        
        if (unit == _selectedUnit)
        {
            if (CurrentMovementStep != MovementStep.SelectingUnit) return false;
            SelectedUnit = null;
            return true;
        }

        if (unit.Owner?.Id != _viewModel.Game?.PhaseStepState?.ActivePlayer.Id) return false;
        if (!CanSelectUnit(unit)) return false;
        
        SelectedUnit = null;
        SelectedUnit = unit;
        return true;
    }

    private void ResetUnitSelection()
    {
        if (_isPostStandupMovement) return;
        lock (_stateLock)
        {
            if (_selectedUnit == null) return;
            _selectedUnit = null;
            _builder.Reset();
            _viewModel.HideMovementPath();
            _viewModel.HideDirectionSelector();
            _viewModel.HideSurfaceSelector();
            ClearHighlighting();
            TransitionTo(new SelectingUnitStep(this));
            _viewModel.NotifySelectedUnitChanged();
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
        
        var availableSurfaces = _reachabilityData.Value
            .GetReachableSurfacesForCoordinate(hex.Coordinates)
            .DistinctBy(s => s.Surface)
            .ToList();

        // If only one surface, proceed directly
        if (availableSurfaces.Count == 1)
        {
            var surface = availableSurfaces[0].Surface;
            _movementPoints = GetRemainingMovementPoints();
            BuildPathsForSurfaceSelection(hex, surface);
            return;
        }

        // Multiple surfaces - pause for user selection
        TransitionTo(new SelectingSurfaceStep(this, hex.Coordinates, availableSurfaces));
    }

    private void BuildPathsForSurfaceSelection(Hex hex, HexSurface surface)
    {
        var startPosition = _selectedPath!.Destination;
        _movementPoints = GetRemainingMovementPoints();
        _possibleDirections = Game!.BattleMap?.GetPathsToHexWithAllFacings(
            startPosition,
            hex.Coordinates,
            _selectedPath!.MovementType,
            _movementPoints,
            _reachabilityData!.Value,
            _selectedUnit!.Height,
            _selectedUnit.MaxLevelChangeForward,
            _selectedUnit.MaxLevelChangeBackward,
            _prohibitedHexes,
            targetSurface: surface) ?? [];

        TransitionTo(new SelectingDirectionStep(this, hex.Coordinates));
    }

    private void HandleSurfaceSelection(HexSurface surface)
    {
        var targetHex = (_step as SelectingSurfaceStep)?.TargetHex;
        if (targetHex == null) return;
        
        _movementPoints = GetRemainingMovementPoints();
        var startPosition = _selectedPath!.Destination;
        _possibleDirections = Game!.BattleMap?.GetPathsToHexWithAllFacings(
            startPosition,
            targetHex,
            _selectedPath!.MovementType,
            _movementPoints,
            _reachabilityData!.Value,
            _selectedUnit!.Height,
            _selectedUnit.MaxLevelChangeForward,
            _selectedUnit.MaxLevelChangeBackward,
            _prohibitedHexes,
            targetSurface: surface) ?? [];

        TransitionTo(new SelectingDirectionStep(this, targetHex));
    }
    
    private void CompleteMovement()
    {
        lock (_stateLock)
        {
            _deferredMovementUnitId = null;
            
            ClearHighlighting();
            _selectedUnit = null;
            _viewModel.HideMovementPath();
            _viewModel.HideDirectionSelector();
            _viewModel.HideSurfaceSelector();
            _viewModel.NotifySelectedUnitChanged();
            _isPostStandupMovement = false; // Reset post-standup state when movement is completed
            TransitionTo(new CompletedStep(this));

            var command = _builder.Build();
            if (command != null && _viewModel.Game is { } clientGame)
            {
                _builder.Reset();
                clientGame.MoveUnit(command.Value);
            }
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

    private string GetPsrProbabilityText(Mech mech, PilotingSkillRollType rollType)
    {
        var psrBreakdown = _viewModel.Game!.PilotingSkillCalculator.GetPsrBreakdown(
            mech, new PilotingSkillRollContext(rollType));
        var probability = Core.Utils.DiceUtils.Calculate2d6Probability(psrBreakdown.ModifiedPilotingSkill);
        return $" ({probability:0}%)";
    }

    private StateAction CreateStayProneAction()
    {
        return new StateAction(
            _viewModel.LocalizationService.GetString("Action_StayProne"),
            true,
            () => HandleMovementTypeSelection(MovementType.StandingStill));
    }

    private StateAction CreateProneFacingChangeAction(Mech mech)
    {
        var availableMp = mech.GetMovementPoints(MovementType.Walk);
        return new StateAction(
            string.Format(_viewModel.LocalizationService.GetString("Action_ChangeFacing"), availableMp),
            true,
            () => HandleProneFacingChange(mech));
    }

    private string GetStandupActionText(MovementType movementType, string probabilityText)
    {
        if (_selectedUnit is Mech { IsMinimumMovement: true })
            return _viewModel.LocalizationService.GetString("Action_AttemptStandup") + probabilityText;

        var typeKey = movementType == MovementType.Run ? "MovementType_Run" : "MovementType_Walk";
        return string.Format(
            _viewModel.LocalizationService.GetString("Action_MovementPoints"),
            _viewModel.LocalizationService.GetString(typeKey),
            _selectedUnit!.GetMovementPoints(movementType)) + probabilityText;
    }

    // TODO that should be a part of UnitPresentationExtensions
    public IEnumerable<StateAction> GetAvailableActions()
    {
        lock (_stateLock)
        {
            if (CurrentMovementStep != MovementStep.SelectingMovementType || _selectedUnit == null)
                return [];

            if (_selectedUnit is Mech { IsProne: true } mech
                && _viewModel.Game is not null)
            {
                // Post-fall: MovementTaken being non-null means the unit was already moving when it fell.
                // Lock the standup to the originally declared movement type
                return mech.MovementTaken != null
                    ? GetLockedProneActions(mech) 
                    : GetStartOfPhaseProneActions(mech);
            }

            return GetNormalActions();
        }
    }

    private List<StateAction> GetLockedProneActions(Mech mech)
    {
        var lockedType = mech.MovementTaken!.MovementType;
        var lockedProneActions = new List<StateAction>
        {
            CreateStayProneAction()
        };

        if (!mech.CanStandup()) return lockedProneActions;

        var probabilityText = GetPsrProbabilityText(mech, PilotingSkillRollType.StandupAttempt);
        lockedProneActions.Add(new StateAction(
            GetStandupActionText(lockedType, probabilityText),
            true,
            () => AttemptStandup(lockedType)));

        if (mech.CanChangeFacingWhileProne())
            lockedProneActions.Add(CreateProneFacingChangeAction(mech));

        return lockedProneActions;
    }

    private List<StateAction> GetStartOfPhaseProneActions(Mech mech)
    {
        var proneActions = new List<StateAction>
        {
            CreateStayProneAction()
        };

        if (mech.IsImmobile) return proneActions;

        if (mech.CanStandup())
        {
            var probabilityText = GetPsrProbabilityText(mech, PilotingSkillRollType.StandupAttempt);

            if (mech.IsMinimumMovement)
            {
                proneActions.Add(new StateAction(
                    _viewModel.LocalizationService.GetString("Action_AttemptStandup") + probabilityText,
                    true,
                    () => AttemptStandup(MovementType.Walk)));
            }
            else
            {
                proneActions.Add(new StateAction(
                    GetStandupActionText(MovementType.Walk, probabilityText),
                    true,
                    () => AttemptStandup(MovementType.Walk)));

                if (mech.CanRun)
                {
                    proneActions.Add(new StateAction(
                        GetStandupActionText(MovementType.Run, probabilityText),
                        true,
                        () => AttemptStandup(MovementType.Run)));
                }
            }
        }

        if (mech.CanChangeFacingWhileProne())
            proneActions.Add(CreateProneFacingChangeAction(mech));

        return proneActions;
    }

    private List<StateAction> GetNormalActions()
    {
        var actions = new List<StateAction>
        {
            new(
                _viewModel.LocalizationService.GetString("Action_StandStill"),
                true,
                () => HandleMovementTypeSelection(MovementType.StandingStill))
        };

        if (_selectedUnit!.IsImmobile) return actions;

        actions.Add(new StateAction(
            string.Format(_viewModel.LocalizationService.GetString("Action_MovementPoints"),
                _viewModel.LocalizationService.GetString("MovementType_Walk"),
                _selectedUnit.GetMovementPoints(MovementType.Walk)),
            true,
            () => HandleMovementTypeSelection(MovementType.Walk)));

        if (_selectedUnit is Mech { CanRun: true })
        {
            actions.Add(new StateAction(
                string.Format(_viewModel.LocalizationService.GetString("Action_MovementPoints"),
                    _viewModel.LocalizationService.GetString("MovementType_Run"),
                    _selectedUnit.GetMovementPoints(MovementType.Run)),
                true,
                () => HandleMovementTypeSelection(MovementType.Run)));
        }

        if (_selectedUnit is not Mech { CanJump: true }) return actions;
        var jumpPoints = _selectedUnit.GetMovementPoints(MovementType.Jump);

        var jumpActionText = string.Format(_viewModel.LocalizationService.GetString("Action_MovementPoints"),
            _viewModel.LocalizationService.GetString("MovementType_Jump"),
            jumpPoints);

        if (_selectedUnit is Mech jumpMech && jumpMech.IsPsrForJumpRequired() && _viewModel.Game is not null)
        {
            jumpActionText += GetPsrProbabilityText(jumpMech, PilotingSkillRollType.JumpWithDamage);
        }

        actions.Add(new StateAction(
            jumpActionText,
            true,
            () => HandleMovementTypeSelection(MovementType.Jump)));

        return actions;
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
                new PathSegment(_selectedUnit.Position, _selectedUnit.Position, [])],
                movementType);
            // Ensure the builder has the unit and movement type set
            _builder.SetUnit(_selectedUnit);
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
            
            // Reset the movement state
            _viewModel.HideDirectionSelector();
            _viewModel.HideSurfaceSelector();
            _viewModel.NotifyStateChanged();
            
            // Publish the command
            _viewModel.Game.TryStandupUnit(standupCommand);
        }
    }

    // Method to resume movement after successful standup
    public void ResumeMovementAfterStandup(Guid unitId)
    {
        lock (_stateLock)
        {
            // Fallback: if _selectedUnit is null (e.g., cleared by ClearSelection during phase change),
            // try to find the unit from the view model
            if (_selectedUnit == null || _selectedUnit.Id != unitId)
            {
                var stoodUpUnit = _viewModel.Units.FirstOrDefault(u => u.Id == unitId);
                if (stoodUpUnit == null)
                {
                    Game?.Logger.LogWarning("ResumeMovementAfterStandup: unit {UnitId} not found among alive units", unitId);
                    return;
                }
                _selectedUnit = stoodUpUnit;
                _builder.SetUnit(stoodUpUnit);
            }
            
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
                new PathSegment(mech.Position, mech.Position, [])],
                _selectedPath.MovementType);
            _builder.SetMovementPath(_selectedPath);
            HighlightReachableHexes();
            TransitionTo(new SelectingTargetHexStep(this));
        }
    }

    public void ResumeMovementAfterFall(Guid unitId)
    {
        lock (_stateLock)
        {
            if (_selectedUnit == null || _selectedUnit.Id != unitId)
            {
                var fallenUnit = _viewModel.Units.FirstOrDefault(u => u.Id == unitId);
                if (fallenUnit == null)
                {
                    Game?.Logger.LogWarning(
                        "ResumeMovementAfterFall: unit {UnitId} not found among alive units", unitId);
                    return;
                }
                _selectedUnit = fallenUnit;
                _builder.SetUnit(fallenUnit);
            }
            
            if (_selectedUnit is not Mech { IsProne: true, Position: not null } mech || _selectedPath == null)
            {
                var exception = new InvalidOperationException("Unit is not prone after fall or no movement path");
                Game?.Logger.LogError(exception, "Unit is not prone after fall or no movement path");
                throw exception;
            }

            if (unitId != mech.Id)
            {
                Game?.Logger.LogWarning(
                    "Resume movement after fall ignored: command unit {CommandUnit} does not match movement state's selected unit {StateUnit}.",
                    unitId,
                    mech.Id);
                return;
            }
            
            _selectedPath = MovementPath.CreateSingleSegmentPath(mech.Position, _selectedPath.MovementType);

            if (!mech.CanStandup())
            {
                _builder.SetUnit(_selectedUnit);
                _builder.SetMovementPath(_selectedPath);
                CompleteMovement();
                return;
            }

            _deferredMovementUnitId = unitId;
            
            TransitionTo(new SelectingMovementTypeStep(this));
            _viewModel.NotifySelectedUnitChanged();
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
            new PathSegment(mech.Position, mech.Position, [])],
            MovementType.Walk);
        _builder.SetMovementPath(_selectedPath);

        void AddToPossibleDirections(HexDirection direction, int cost)
        {
            var pathSegments = new MovementPath([
                new PathSegment(mech.Position, mech.Position with { Facing = direction }, [new RotationMovementCost { FromFacing = mech.Position.Facing, ToFacing = direction, Value = cost }])
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
                var path = MovementPath.CreateSingleSegmentPath(State._selectedUnit.Position);
                State._builder.SetMovementPath(path);
                State.CompleteMovement();
                return;
            }

            State._selectedPath = new MovementPath([
                    new PathSegment(State._selectedUnit.Position, State._selectedUnit.Position, [])],
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

    private sealed class SelectingSurfaceStep : MovementStepBase
    {
        public HexCoordinates TargetHex { get; }

        public SelectingSurfaceStep(MovementState state, HexCoordinates targetHex, IReadOnlyList<HexReachabilityData> availableSurfaces) : base(state)
        {
            TargetHex = targetHex;

            var vm = new SurfaceSelectorViewModel(
                availableSurfaces,
                surface =>
                {
                    State._viewModel.HideSurfaceSelector();
                    State.HandleSurfaceSelection(surface);
                },
                State._viewModel.LocalizationService,
                () =>
                {
                    State._viewModel.HideSurfaceSelector();
                    State.TransitionTo(new SelectingTargetHexStep(State));
                });
            State._viewModel.ShowSurfaceSelector(targetHex, vm);
            State._viewModel.NotifyStateChanged();
        }
        public override MovementStep Step => MovementStep.SelectingSurface;
        public override string ActionLabel => State._viewModel.LocalizationService.GetString("Action_SelectSurface");
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
            State.Game?.Logger.LogInformation("[TEMP] SelectingStandingUpDirectionStep.HandleFacingSelection: direction={Direction}", direction);
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
