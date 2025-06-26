using Sanet.MakaMek.Core.Data.Game.Commands.Client.Builders;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Presentation.UiStates;

public class MovementState : IUiState
{
    private readonly BattleMapViewModel _viewModel;
    private readonly MoveUnitCommandBuilder _builder;
    private Unit? _selectedUnit;
    private List<HexCoordinates> _forwardReachableHexes = [];
    private List<HexCoordinates> _backwardReachableHexes = [];
    private readonly List<HexCoordinates> _prohibitedHexes;
    private MovementType? _selectedMovementType;
    private int _movementPoints;
    private Dictionary<HexDirection, List<PathSegment>> _possibleDirections = [];
    private readonly Lock _stateLock = new();

    public MovementState(BattleMapViewModel viewModel)
    {
        _viewModel = viewModel;
        if (_viewModel.Game == null)
        {
            throw new InvalidOperationException("Game is null"); 
        }
        if (_viewModel.Game.ActivePlayer == null)
        {
            throw new InvalidOperationException("Active player is null"); 
        }
        _builder = new MoveUnitCommandBuilder(_viewModel.Game.Id, _viewModel.Game.ActivePlayer.Id);
        
        // Get hexes with enemy units - these will be excluded from pathfinding
        _prohibitedHexes = _viewModel.Units
            .Where(u=>u.Owner?.Id != _viewModel.Game.ActivePlayer?.Id && u.Position!=null)
            .Select(u => u.Position!.Coordinates)
            .ToList();
    }

    public void HandleUnitSelection(Unit? unit)
    {
        lock (_stateLock)
        {
            if (_viewModel.Game is { CanActivePlayerAct: false }) return;
            if (unit == null) return;
            if (unit.Status == UnitStatus.Destroyed) return;
            if (unit.HasMoved) return;
            
            _selectedUnit = unit;
            _builder.SetUnit(unit);
            CurrentMovementStep = MovementStep.SelectingMovementType;
            _viewModel.NotifyStateChanged();
        }
    }

    public void HandleMovementTypeSelection(MovementType movementType)
    {
        if (_viewModel.Game is { CanActivePlayerAct: false }) return;
        if (_selectedUnit == null) return;
        if (CurrentMovementStep != MovementStep.SelectingMovementType) return;
        _selectedMovementType = movementType;
        _builder.SetMovementType(movementType);
        
        if (movementType == MovementType.StandingStill)
        {
            // For standing still, we create a single path segment with same From and To positions
            var path = new List<PathSegment>();
            _builder.SetMovementPath(path);
            CompleteMovement();
            return;
        }

        CurrentMovementStep = MovementStep.SelectingTargetHex;
        _movementPoints = _selectedUnit?.GetMovementPoints(movementType) ?? 0;

        // Get reachable hexes and highlight them
        if (_selectedUnit?.Position != null && _viewModel.Game?.BattleMap != null)
        {
            if (movementType == MovementType.Jump)
            {
                // For jumping, we use the simplified method that ignores terrain and facing
                var reachableHexes = _viewModel.Game.BattleMap
                    .GetJumpReachableHexes(
                        _selectedUnit.Position.Coordinates,
                        _movementPoints,
                        _prohibitedHexes)
                    .Where(hex => !_viewModel.Units
                        .Any(u => u.Owner?.Id == _viewModel.Game.ActivePlayer?.Id && u.Position?.Coordinates == hex))
                    .ToList();
                
                // For jumping, there's no forward/backward distinction
                _forwardReachableHexes = reachableHexes;
                _backwardReachableHexes = [];
            }
            else
            {
                // Get forward reachable hexes
                _forwardReachableHexes = _viewModel.Game.BattleMap
                    .GetReachableHexes(_selectedUnit.Position, _movementPoints, _prohibitedHexes)
                    .Select(x => x.coordinates)
                    .Where(hex => !_viewModel.Units
                        .Any(u => u != _selectedUnit 
                            && u.Owner?.Id == _viewModel.Game.ActivePlayer?.Id 
                            && u.Position?.Coordinates == hex))
                    .ToList();

                // Get backward reachable hexes if unit can move backward
                _backwardReachableHexes = [];
                if (_selectedUnit.CanMoveBackward(movementType))
                {
                    var oppositePosition = _selectedUnit.Position.GetOppositeDirectionPosition();
                    _backwardReachableHexes = _viewModel.Game.BattleMap
                        .GetReachableHexes(oppositePosition, _movementPoints, _prohibitedHexes)
                        .Select(x => x.coordinates)
                        .Where(hex => !_viewModel.Units
                            .Any(u => u != _selectedUnit 
                                && u.Owner?.Id == _viewModel.Game.ActivePlayer?.Id 
                                && u.Position?.Coordinates == hex))
                        .ToList();
                }
            }

            // Highlight all reachable hexes
            var allReachableHexes = _forwardReachableHexes.Union(_backwardReachableHexes).ToList();
            _viewModel.HighlightHexes(allReachableHexes, true);
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
        if (CurrentMovementStep == MovementStep.ConfirmMovement)
        {
            ConfirmMovement();
        }
        if (CurrentMovementStep != MovementStep.SelectingDirection) return;
        var path = _possibleDirections[direction]; 
        _builder.SetMovementPath(path);
        _viewModel.ShowDirectionSelector(path.Last().To.Coordinates, [direction]);
        _viewModel.ShowMovementPath(path);
        CurrentMovementStep = MovementStep.ConfirmMovement;
        _viewModel.NotifyStateChanged();
    }

    private void ConfirmMovement()
    {
        if (CurrentMovementStep != MovementStep.ConfirmMovement) return;
        var direction = _viewModel.AvailableDirections?.FirstOrDefault();
        if (direction == null) return; 
        var path = _possibleDirections[direction.Value];
        if (_viewModel.MovementPath == null || _viewModel.MovementPath.Last().To != path.Last().To) return;
        
        CompleteMovement();
    }

    private bool HandleUnitSelectionFromHex(Hex hex)
    {
        var unit = _viewModel.Units.FirstOrDefault(u => u.Position?.Coordinates == hex.Coordinates);
        if (unit == null 
            || unit == _selectedUnit
            || unit.Owner?.Id != _viewModel.Game?.ActivePlayer?.Id) return false;
        ResetUnitSelection();
        _viewModel.SelectedUnit=unit;
        return true;
    }

    private void ResetUnitSelection()
    {
        lock (_stateLock)
        {
            if (_viewModel.SelectedUnit == null) return;
            _viewModel.SelectedUnit = null;
            _selectedUnit = null;
            _viewModel.HideMovementPath();
            _viewModel.HideDirectionSelector();
            if (_forwardReachableHexes.Count > 0 || _backwardReachableHexes.Count > 0)
            {
                var allReachableHexes = _forwardReachableHexes.Union(_backwardReachableHexes).ToList();
                _viewModel.HighlightHexes(allReachableHexes, false);
                _forwardReachableHexes = [];
                _backwardReachableHexes = [];
            }
            CurrentMovementStep=MovementStep.SelectingUnit;
            _viewModel.NotifyStateChanged();
        }
    }

    private void HandleTargetHexSelection(Hex hex)
    {
        if (_selectedUnit?.Position == null || _viewModel.Game == null) return;

        var isForwardReachable = _forwardReachableHexes.Contains(hex.Coordinates);
        var isBackwardReachable = _backwardReachableHexes.Contains(hex.Coordinates);
        
        // Reset selection if clicked outside reachable hexes during target hex selection
        if (!isForwardReachable && !isBackwardReachable)
        {
            ResetUnitSelection();
            return;
        }

        if (!isForwardReachable && !isBackwardReachable) return;

        CurrentMovementStep = MovementStep.SelectingDirection;
        
        _possibleDirections = [];
        var availableDirections = Enum.GetValues<HexDirection>();

        if (_selectedMovementType == MovementType.Jump)
        {
            // For jumping, we can face any direction and calculate path ignoring terrain
            foreach (var direction in availableDirections)
            {
                var targetPos = new HexPosition(hex.Coordinates, direction);
                var path = _viewModel.Game.BattleMap?.FindJumpPath(
                    _selectedUnit.Position,
                    targetPos,
                    _movementPoints);

                if (path != null)
                {
                    _possibleDirections[direction] = path;
                }
            }
        }
        else
        {
            foreach (var direction in availableDirections)
            {
                var targetPos = new HexPosition(hex.Coordinates, direction);
                List<PathSegment>? path = null;

                if (isForwardReachable)
                {
                    // Try forward movement
                    path = _viewModel.Game?.BattleMap?.FindPath(
                        _selectedUnit.Position,
                        targetPos,
                        _movementPoints,
                        _prohibitedHexes);
                }

                if (path == null && isBackwardReachable)
                {
                    // Try backward movement
                    var oppositeStartPos = _selectedUnit.Position.GetOppositeDirectionPosition();
                    var oppositeTargetPos = targetPos.GetOppositeDirectionPosition();
                    
                    path = _viewModel.Game?.BattleMap?.FindPath(
                        oppositeStartPos,
                        oppositeTargetPos,
                        _movementPoints,
                        _prohibitedHexes);

                    // If path found, swap all directions in path segments
                    path = path?.Select(segment => new PathSegment(
                        new HexPosition(segment.From.Coordinates, segment.From.Facing.GetOppositeDirection()),
                        new HexPosition(segment.To.Coordinates, segment.To.Facing.GetOppositeDirection()),
                        segment.Cost
                    )).ToList();
                }

                if (path != null)
                {
                    _possibleDirections[direction] = path;
                }
            }
        }

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
            var allReachableHexes = _forwardReachableHexes.Union(_backwardReachableHexes).ToList();
            _viewModel.HighlightHexes(allReachableHexes,false);
            _forwardReachableHexes = [];
            _backwardReachableHexes = [];
            _selectedUnit = null;
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
        _ => string.Empty
    };

    public bool IsActionRequired =>
        _viewModel.Game is { CanActivePlayerAct: true } &&
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
                // Calculate piloting skill roll breakdown and success probability
                var psrBreakdown = _viewModel.Game.PilotingSkillCalculator.GetPsrBreakdown(
                    mech, PilotingSkillRollType.GyroHit);
                
                var successProbability = Core.Utils.DiceUtils.Calculate2d6Probability(psrBreakdown.ModifiedPilotingSkill);
                
                // Format the probability as percentage
                var probabilityText = $" ({successProbability:0}%)";
                
                // If the Mech is prone, only offer the "Attempt Standup" action if it can stand up
                if (mech.CanStandup())
                {
                    return [new StateAction(
                        _viewModel.LocalizationService.GetString("Action_AttemptStandup") + probabilityText,
                        true,
                        () => HandleStandupAttempt(mech))];
                }
                
                // If Mech can't stand up, return empty actions list
                return [];
            }

            var actions = new List<StateAction>
            {
                // Stand Still
                new(
                    _viewModel.LocalizationService.GetString("Action_StandStill"),
                    true,
                    () => HandleMovementTypeSelection(MovementType.StandingStill)),
                // Walk
                new(
                    string.Format(_viewModel.LocalizationService.GetString("Action_MovementPoints"), 
                        _viewModel.LocalizationService.GetString("MovementType_Walk"), 
                        _selectedUnit.GetMovementPoints(MovementType.Walk)),
                    true,
                    () => HandleMovementTypeSelection(MovementType.Walk)),
                // Run
                new(
                    string.Format(_viewModel.LocalizationService.GetString("Action_MovementPoints"), 
                        _viewModel.LocalizationService.GetString("MovementType_Run"), 
                        _selectedUnit.GetMovementPoints(MovementType.Run)),
                    true,
                    () => HandleMovementTypeSelection(MovementType.Run))
            };

            // Jump
            var jumpPoints = _selectedUnit.GetMovementPoints(MovementType.Jump);
            if (jumpPoints > 0)
            {
                actions.Add(new StateAction(
                    string.Format(_viewModel.LocalizationService.GetString("Action_MovementPoints"), 
                        _viewModel.LocalizationService.GetString("MovementType_Jump"), 
                        jumpPoints),
                    true,
                    () => HandleMovementTypeSelection(MovementType.Jump)));
            }

            return actions;
        }
    }

    // New method to handle standup attempts
    private void HandleStandupAttempt(Mech mech)
    {
        if (_viewModel.Game?.ActivePlayer == null) return;
        
        // Create a standup command
        var standupCommand = new TryStandupCommand
        {
            GameOriginId = _viewModel.Game.Id,
            UnitId = mech.Id,
            PlayerId = _viewModel.Game.ActivePlayer.Id
        };
        
        // Publish the command
        _viewModel.Game.TryStandupUnit(standupCommand);
    }
}
