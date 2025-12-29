using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Bots.Exceptions;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Bots.Models.DecisionEngines;

/// <summary>
/// Decision engine for the movement phase
/// </summary>
public class MovementEngine : IBotDecisionEngine
{
    private readonly IClientGame _clientGame;
    private readonly ITacticalEvaluator _evaluator;

    public MovementEngine(IClientGame clientGame, ITacticalEvaluator evaluator)
    {
        _clientGame = clientGame;
        _evaluator = evaluator;
    }

    public async Task MakeDecision(IPlayer player)
    {
        IUnit? unitToMove = null;
        try
        {
            // 1. Get all friendly units that haven't moved
            var myUnitsToMove = player.AliveUnits
                .Where(u => u is { HasMoved: false })
                .ToList();

            if (myUnitsToMove.Count == 0)
            {
                // No units to move
                Console.WriteLine($"[MovementEngine] No units to move for player {player.Name}, skipping...");
                return;
            }
            
            var immobileUnit = myUnitsToMove.FirstOrDefault(u => u.IsImmobile);
            if (immobileUnit != null)
            {
                await SkipTurn(player, immobileUnit);
                return;
            }

            // 2. Calculate enemy units and friendly positions once (static during this decision)
            var enemyUnits = _clientGame.Players
                .Where(p => p.Id != player.Id)
                .SelectMany(p => p.AliveUnits)
                .Where(u => !u.Status.HasFlag(UnitStatus.Destroyed))
                .ToList();

            var friendlyPositions = player.AliveUnits
                .Where(u => u is { IsDeployed: true, Position: not null })
                .Select(u => u.Position!.Coordinates)
                .ToHashSet();

            // 3. Analyze Phase State
            var enemyUnitsToMoveCount = enemyUnits.Count(u => !u.HasMoved);

            var state = new MovementPhaseState(
                EnemyUnitsRemaining: enemyUnitsToMoveCount
            );

            // 4. Calculate Priorities
            var scoredUnits = myUnitsToMove.Select(u => new
                {
                    Unit = u,
                    Priority = CalculateUnitPriority(u, state)
                })
                .OrderByDescending(u => u.Priority)
                .ThenByDescending(u => u.Unit.Id) // Deterministic tie-breaker
                .ToList();

            var bestCandidate = scoredUnits.First();
            unitToMove = bestCandidate.Unit;

            Console.WriteLine($"[MovementEngine] Selected {unitToMove.Name} (Role: {unitToMove.GetTacticalRole()}, Priority: {bestCandidate.Priority})");

            // 5. Execute Move for a selected unit
            await ExecuteMoveForUnit(player, unitToMove, enemyUnits, friendlyPositions);
        }
        catch (BotDecisionException ex)
        {
            // Rethrow BotDecisionException to let caller handle decision failures
            Console.WriteLine($"MovementEngine error for player {player.Name}: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            // Log error but don't throw - graceful degradation for unexpected errors
            Console.WriteLine($"MovementEngine error for player {player.Name}: {ex.Message}, skipping turn");
            await SkipTurn(player, unitToMove);
        }
    }

    private double CalculateUnitPriority(IUnit unit, MovementPhaseState state)
    {
        double priority = 0;

        // 1. Role Score
        var role = unit.GetTacticalRole();
        
        // Handle Fallen/Prone units
        if (unit is Mech { IsProne: true })
        {
            return 0; // Always last
        }

        priority += role switch
        {
            UnitTacticalRole.LrmBoat => 90,
            UnitTacticalRole.Scout => 20,
            UnitTacticalRole.Jumper => 25,
            UnitTacticalRole.Brawler => 30,
            _ => 50 // Default for others
        };

        // Check for 0 movement options (if detectable easily, for now relying on roles)
        // If we want to strictly follow "Units with 0 movement options: 80", we'd need to check reachability.
        // Skipping expensive check for now as per "0 for now will be introduced later" for situation modifier, 
        // effectively treating "0 options" as a situation.

        // 2. Initiative Modifier
        if (state.EnemyUnitsRemaining == 0)
        {
            // We are moving last/late (no enemies left to move)
            priority += 30;
        }
        else
        {
            // Enemies still have moves
            if (role == UnitTacticalRole.Brawler)
            {
                priority -= 30;
            }
        }

        return priority;
    }

    /// <summary>
    /// Executes movement for a unit using intelligent position evaluation
    /// </summary>
    private async Task ExecuteMoveForUnit(
        IPlayer player, 
        IUnit unit, 
        IReadOnlyList<IUnit> enemyUnits,
        IReadOnlySet<HexCoordinates> friendlyPositions)
    {
        // Handle prone mechs - try to stand up
        if (unit is Mech { IsProne: true } mech && mech.CanStandup())
        {
            await AttemptStandup(player, mech);
            return;
        }

        if (_clientGame.BattleMap == null || unit.Position == null)
        {
            await SkipTurn(player, unit);
            return;
        }

        // Get occupied hexes (exclude the moving unit itself)
        var occupiedHexes = GetOccupiedHexes(unit);

        // Determine available movement types for this unit
        var availableMovementTypes = new List<MovementType> { MovementType.Walk };
        
        if (unit is Mech mechUnit)
        {
            if (mechUnit.CanRun)
                availableMovementTypes.Add(MovementType.Run);
            if (mechUnit.CanJump)
                availableMovementTypes.Add(MovementType.Jump);
        }
        else if (unit.GetMovementPoints(MovementType.Run) > 0)
        {
            availableMovementTypes.Add(MovementType.Run);
        }

        // Evaluate all candidate positions with all available movement types
        var candidateScores = new List<PositionScore>();

        foreach (var movementType in availableMovementTypes)
        {
            // Get all reachable hexes for this movement type
            var reachabilityData = _clientGame.BattleMap.GetReachableHexesForUnit(
                unit,
                movementType,
                occupiedHexes,
                friendlyPositions);

            var reachablePaths = new List<MovementPath>();

            // Process all reachable hexes (both forward and backward)
            foreach (var hex in reachabilityData.AllReachableHexes)
            {
                var paths = _clientGame.BattleMap.GetPathsToHexWithAllFacings(
                    unit.Position,
                    hex,
                    movementType,
                    unit.GetMovementPoints(movementType),
                    reachabilityData,
                    occupiedHexes);

                reachablePaths.AddRange(paths.Values);
            }

            // Evaluate each path
            foreach (var path in reachablePaths)
            {
                var score = _evaluator.EvaluatePath(
                    unit,
                    path,
                    enemyUnits);

                candidateScores.Add(score);
            }
        }

        // If no valid paths, stand still
        if (candidateScores.Count == 0)
        {
            await SkipTurn(player, unit);
            return;
        }

        // Select the path with the best combined score
        var bestScore = candidateScores
            .OrderByDescending(s => s.GetCombinedScore())
            .First();

        Console.WriteLine($"[MovementEngine] {unit.Name} moving to {bestScore.Position.Coordinates} " +
                         $"using {bestScore.MovementType} (Offensive: {bestScore.OffensiveIndex:F1}, " +
                         $"Defensive: {bestScore.DefensiveIndex:F1}, Combined: {bestScore.GetCombinedScore():F1})");

        // Execute the move using the stored path (no need to recalculate)
        await MoveUnit(player, unit, bestScore.Path);
    }

    /// <summary>
    /// Gets the coordinates of all occupied hexes (deployed units except the specified moving unit)
    /// </summary>
    private IReadOnlySet<HexCoordinates> GetOccupiedHexes(IUnit movingUnit)
    {
        return _clientGame.Players
            .SelectMany(p => p.Units)
            .Where(u => u is { IsDeployed: true, Position: not null } && u.Id != movingUnit.Id)
            .Select(u => u.Position!.Coordinates)
            .ToHashSet();
    }

    private async Task AttemptStandup(IPlayer player, Mech mech)
    {
        // Select a random facing direction
        var newFacing = HexDirectionExtensions.AllDirections[Random.Shared.Next(HexDirectionExtensions.AllDirections.Length)];

        var command = new TryStandupCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = player.Id,
            UnitId = mech.Id,
            NewFacing = newFacing,
            MovementTypeAfterStandup = MovementType.Walk
        };

        await _clientGame.TryStandupUnit(command);
    }

    private async Task MoveUnit(IPlayer player, IUnit unit, MovementPath path)
    {
        var command = new MoveUnitCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = player.Id,
            UnitId = unit.Id,
            MovementType = path.MovementType,
            MovementPath = path.ToData()
        };

        await _clientGame.MoveUnit(command);
    }

    private async Task SkipTurn(IPlayer player, IUnit? unit)
    {
        // Find any unit that hasn't moved yet (fallback)
        var unmovedUnit = unit ?? player.AliveUnits.FirstOrDefault(u => !u.HasMoved);
        var position = unmovedUnit?.Position;
        if (unmovedUnit == null || position == null)
        {
            throw new BotDecisionException(
                $"No unmoved units available for player {player.Name}",
                nameof(MovementEngine),
                player.Id);
        }

        // Send a StandingStill movement command
        await MoveUnit(player, unmovedUnit, MovementPath.CreateStandingStillPath(position));
    }
}


