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
    private MovementType? _selectedMovementType;
    private int _movementPoints;
    private Dictionary<HexDirection, MovementPath> _possibleDirections = [];
    private readonly Lock _stateLock = new();
    private bool _isPostStandupMovement;

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
            CurrentMovementStep = MovementStep.SelectingMovementType;
            _viewModel.NotifyStateChanged();
        }
    }

    public void HandleMovementTypeSelection(MovementType movementType)
    {
        lock (_stateLock)
        {
            if (!this.CanHumanPlayerAct()) return;
            if (_selectedUnit == null) return;
            if (CurrentMovementStep != MovementStep.SelectingMovementType) return;
            _selectedMovementType = movementType;
            _builder.SetMovementType(movementType);

            HighlightReachableHexes();
        }
    }

    private void HighlightReachableHexes()
    {
        if (_selectedMovementType == null) return;
        var position = _selectedUnit?.Position;
        if (position == null) return;
        var movementType = _selectedMovementType.Value;
        if (movementType == MovementType.StandingStill)
        {
            // For standing still, we create an empty movement path
            var path =MovementPath.CreateStandingStillPath(position);
            _builder.SetMovementPath(path);
            CompleteMovement();
            return;
        }

        CurrentMovementStep = MovementStep.SelectingTargetHex;
        _movementPoints = _selectedUnit?.GetMovementPoints(movementType) ?? 0;

        // Get reachable hexes and highlight them
        if (_selectedUnit != null && _viewModel.Game?.BattleMap != null)
        {
            _reachabilityData = _viewModel.Game.BattleMap.GetReachableHexesForUnit(
                _selectedUnit,
                movementType,
                _prohibitedHexes,
                _friendlyUnitsCoordinates
            );
            
            _viewModel.HighlightHexes(_reachabilityData.Value.AllReachableHexes, true);
        }

        _viewModel.NotifyStateChanged();
    }

    public void HandleHexSelection(Hex hex)
    {
        if (HandleUnitSelectionFromHex(hex)) return;
        HandleTargetHexSelection(hex);
    }

    public void HandleFacingSelection(HexDirection direction)
    {
        lock (_stateLock)
        {
            if (CurrentMovementStep == MovementStep.ConfirmMovement)
            {
                ConfirmMovement();
                return;
            }

            // Check if this is a standup direction selection
            if (CurrentMovementStep == MovementStep.SelectingStandingUpDirection)
            {
                // This is standup with direction selection - send the standup command immediately
                CompleteStandupAttempt(direction);
                return;
            }

            if (CurrentMovementStep != MovementStep.SelectingDirection) return;

            var path = _possibleDirections[direction];
            
            _builder.SetMovementPath(path);
            _viewModel.ShowDirectionSelector(path.Destination.Coordinates, [direction]);
            _viewModel.ShowMovementPath(path);
            CurrentMovementStep = MovementStep.ConfirmMovement;
            _viewModel.NotifyStateChanged();
        }
    }

    private void ConfirmMovement()
    {
        if (CurrentMovementStep != MovementStep.ConfirmMovement) return;
        var direction = _viewModel.AvailableDirections?.FirstOrDefault();
        if (direction == null) return; 
        var path = _possibleDirections[direction.Value];
        if (_viewModel.MovementPath == null || _viewModel.MovementPath.Last().To != path.Destination) return;
        
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
            if (_reachabilityData is { } data 
                && (data.ForwardReachableHexes.Count > 0 
                    || data.BackwardReachableHexes.Count > 0))
            {
                _viewModel.HighlightHexes(_reachabilityData.Value.AllReachableHexes,false);
                _reachabilityData = null;
            }
            CurrentMovementStep=MovementStep.SelectingUnit;
            _viewModel.NotifyStateChanged();
        }
    }

    private void HandleTargetHexSelection(Hex hex)
    {
        if (_selectedUnit?.Position == null
            || _viewModel.Game == null
            || _selectedMovementType == null
            || _reachabilityData == null) return;

        // Reset selection if clicked outside reachable hexes during target hex selection
        // BUT NOT if the unit is in post-standup movement state (must complete declared movement)
        if (!_reachabilityData.Value.IsHexReachable(hex.Coordinates))
        {
            ResetUnitSelection();
            return;
        }

        CurrentMovementStep = MovementStep.SelectingDirection;

        // Use the extension method to find all possible paths to the target hex
        _possibleDirections = _viewModel.Game.BattleMap?.GetPathsToHexWithAllFacings(
            _selectedUnit.Position,
            hex.Coordinates,
            _selectedMovementType.Value,
            _movementPoints,
            _reachabilityData.Value,
            _prohibitedHexes) ?? [];

        // Show direction selector if there are any possible directions
        if (_possibleDirections.Count != 0)
        {
            _viewModel.HideMovementPath();
            _viewModel.ShowDirectionSelector(hex.Coordinates, _possibleDirections.Select(kv=>kv.Key).ToList());
        }

        _viewModel.NotifyStateChanged();
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
            if (_reachabilityData != null)
                _viewModel.HighlightHexes(_reachabilityData.Value.AllReachableHexes,false);
            _reachabilityData = null;
            _selectedUnit = null;
            _isPostStandupMovement = false; // Reset post-standup state when movement is completed
            CurrentMovementStep = MovementStep.Completed;
            _viewModel.NotifyStateChanged();
        }
    }

    public string ActionLabel => !IsActionRequired? string.Empty : CurrentMovementStep switch
    {
        MovementStep.SelectingUnit => _viewModel.LocalizationService.GetString("Action_SelectUnitToMove"),
        MovementStep.SelectingMovementType => _viewModel.LocalizationService.GetString("Action_SelectMovementType"),
        MovementStep.SelectingTargetHex => _viewModel.LocalizationService.GetString("Action_SelectTargetHex"),
        MovementStep.SelectingDirection => _viewModel.LocalizationService.GetString("Action_SelectFacingDirection"),
        MovementStep.ConfirmMovement => _viewModel.LocalizationService.GetString("Action_MoveUnit"),
        MovementStep.SelectingStandingUpDirection => _viewModel.LocalizationService.GetString("Action_SelectFacingDirection"),
        _ => string.Empty
    };

    public bool IsActionRequired =>
        this.CanHumanPlayerAct() &&
        CurrentMovementStep != MovementStep.Completed;
    
    public bool CanExecutePlayerAction => CurrentMovementStep == MovementStep.ConfirmMovement;
    
    public string PlayerActionLabel => CurrentMovementStep == MovementStep.ConfirmMovement ? 
        _viewModel.LocalizationService.GetString("Action_MoveUnit") : string.Empty;
    
    public MovementStep CurrentMovementStep { get; private set; } = MovementStep.SelectingUnit;

    public void ExecutePlayerAction()
    {
        if (CurrentMovementStep == MovementStep.ConfirmMovement)
        {
            ConfirmMovement();
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
                        // Minimum movement case: single "Attempt Standup" button, automatically use running
                        proneActions.Add(new StateAction(
                            _viewModel.LocalizationService.GetString("Action_AttemptStandup") + probabilityText,
                            true,
                            () => AttemptStandup(MovementType.Run)));
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

    // New method to handle standup attempts
    private void AttemptStandup(MovementType movementType)
    {
        lock (_stateLock)
        {
            if (_viewModel.Game?.PhaseStepState?.ActivePlayer == null) return;
            if (_selectedUnit?.Position == null) return;

            // Set the selected movement type for later use
            _selectedMovementType = movementType;
            // Ensure the builder has the movement type set
            _builder.SetMovementType(_selectedMovementType.Value);

            CurrentMovementStep = MovementStep.SelectingStandingUpDirection;
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
            if (_selectedMovementType == null) return;

            // Create a standup command with the selected direction
            var standupCommand = new TryStandupCommand
            {
                GameOriginId = _viewModel.Game.Id,
                UnitId = _selectedUnit.Id,
                PlayerId = _viewModel.Game.PhaseStepState.Value.ActivePlayer.Id,
                NewFacing = direction,
                MovementTypeAfterStandup = _selectedMovementType.Value
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
            if (_selectedMovementType != null &&
                _selectedUnit is Mech { IsProne: false } mech
                && mech.GetMovementPoints(_selectedMovementType.Value) > 0)
            {
                _isPostStandupMovement = true; // Mark that this unit is in post-standup movement state
                HighlightReachableHexes();
            }
        }
    }

    // New method to handle prone facing change
    private void HandleProneFacingChange(Mech mech)
    {
        if (_viewModel.Game?.PhaseStepState?.ActivePlayer == null) return;
        if (mech.Position == null || !mech.IsProne) return;

        // Set up for prone facing change movement
        _builder.SetMovementType(MovementType.Walk);
        _movementPoints = mech.GetMovementPoints(MovementType.Walk);

        // Calculate maximum rotation steps based on available movement points
        var maxRotateSteps = Math.Min(3, _movementPoints);

        // Reset possible directions
        _possibleDirections = [];

        var currentFacing = mech.Facing;
        if (currentFacing == null) return;

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

        CurrentMovementStep = MovementStep.SelectingDirection;
        _viewModel.ShowDirectionSelector(mech.Position.Coordinates, _possibleDirections.Keys);
        _viewModel.NotifyStateChanged();
    }
}
