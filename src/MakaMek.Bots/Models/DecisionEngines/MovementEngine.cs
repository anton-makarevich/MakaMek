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

    public MovementEngine(IClientGame clientGame)
    {
        _clientGame = clientGame;
    }

    public async Task MakeDecision(IPlayer player)
    {
        try
        {
            // 1. Get all friendly units that haven't moved
            var myUnitsToMove = player.AliveUnits
                .Where(u => !u.HasMoved && !u.IsImmobile)
                .ToList();

            if (myUnitsToMove.Count == 0)
            {
                // No units to move, skip turn (or safeguard if called when no moves left)
                await SkipTurn(player);
                return;
            }

            // 2. Analyze Phase State
            var enemyUnitsToMoveCount = _clientGame.Players
                .Where(p => p.Id != player.Id)
                .SelectMany(p => p.AliveUnits)
                .Count(u => !u.HasMoved && !u.Status.HasFlag(UnitStatus.Destroyed));

            var totalMyUnits = player.AliveUnits.Count;
            var unitsRemainingRatio = (double)myUnitsToMove.Count / totalMyUnits;
            var phase = unitsRemainingRatio switch
            {
                > 0.7 => PhaseState.Early,
                < 0.3 => PhaseState.Late,
                _ => PhaseState.Mid
            };

            var state = new MovementPhaseState(
                EnemyUnitsRemaining: enemyUnitsToMoveCount,
                FriendlyUnitsRemaining: myUnitsToMove.Count,
                Phase: phase
            );

            // 3. Calculate Priorities
            var scoredUnits = myUnitsToMove.Select(u => new
                {
                    Unit = u,
                    Priority = CalculateUnitPriority(u, state)
                })
                .OrderByDescending(u => u.Priority)
                .ThenByDescending(u => u.Unit.Id) // Deterministic tie-breaker
                .ToList();

            var bestCandidate = scoredUnits.First();
            var unitToMove = bestCandidate.Unit;

            Console.WriteLine($"[MovementEngine] Selected {unitToMove.Name} (Role: {unitToMove.GetTacticalRole()}, Priority: {bestCandidate.Priority})");

            // 4. Execute Move for selected unit
            await ExecuteMoveForUnit(player, unitToMove);
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
            await SkipTurn(player);
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

    private async Task ExecuteMoveForUnit(IPlayer player, IUnit unit)
    {
        // Handle prone mechs - try to stand up
        if (unit is Mech { IsProne: true } mech && mech.CanStandup())
        {
            await AttemptStandup(player, mech);
            return;
        }

        // Cache occupied hexes for this decision cycle (won't change during this invocation)
        var occupiedHexes = GetOccupiedHexes(unit);

        // Select movement type (prefer Walk for Easy difficulty)
        var movementType = SelectMovementType(unit);

        // Get a random valid destination
        var destination = GetRandomDestination(unit, movementType, occupiedHexes);
        if (destination == null)
        {
            // No valid destination, just stand still
            await MoveUnit(player, unit, MovementType.StandingStill, []);
            return;
        }

        // Find path to destination
        var path = FindPath(unit, destination, movementType, occupiedHexes);
        if (path == null || path.Count == 0)
        {
            // No valid path, just stand still
            await MoveUnit(player, unit, MovementType.StandingStill, []);
            return;
        }

        // Move the unit
        await MoveUnit(player, unit, movementType, path);
    }

    private async Task AttemptStandup(IPlayer player, Mech mech)
    {
        // Select random facing direction
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

    private static MovementType SelectMovementType(IUnit unit)
    {
        // 80% Walk, 20% Run for now
        const double walkChance = 0.8;

        if (Random.Shared.NextDouble() < walkChance)
        {
            return MovementType.Walk;
        }

        // Check if unit can run
        return unit.GetMovementPoints(MovementType.Run) > 0 
            ? MovementType.Run 
            : MovementType.Walk;
    }

    /// <summary>
    /// Gets the coordinates of all occupied hexes (deployed units except the specified moving unit)
    /// </summary>
    private List<HexCoordinates> GetOccupiedHexes(IUnit movingUnit)
    {
        return _clientGame.Players
            .SelectMany(p => p.Units)
            .Where(u => u is { IsDeployed: true, Position: not null } && u.Id != movingUnit.Id)
            .Select(u => u.Position!.Coordinates)
            .ToList();
    }

    private HexPosition? GetRandomDestination(IUnit unit, MovementType movementType, List<HexCoordinates> occupiedHexes)
    {
        if (_clientGame.BattleMap == null || unit.Position == null)
            return null;

        var movementPoints = unit.GetMovementPoints(movementType);
        if (movementPoints <= 0)
            return null;

        // Get reachable hexes
        var reachableHexes = _clientGame.BattleMap
            .GetReachableHexes(unit.Position, movementPoints, occupiedHexes)
            .Select(h => h.coordinates)
            .ToList();

        if (reachableHexes.Count == 0)
            return null;

        // Select random hex
        var targetCoordinates = reachableHexes[Random.Shared.Next(reachableHexes.Count)];

        // Select random facing
        var targetFacing = HexDirectionExtensions.AllDirections[Random.Shared.Next(HexDirectionExtensions.AllDirections.Length)];

        return new HexPosition(targetCoordinates, targetFacing);
    }

    private List<PathSegment>? FindPath(IUnit unit, HexPosition destination, MovementType movementType, List<HexCoordinates> occupiedHexes)
    {
        if (_clientGame.BattleMap == null || unit.Position == null)
            return null;

        var movementPoints = unit.GetMovementPoints(movementType);

        return _clientGame.BattleMap.FindPath(unit.Position, destination, movementPoints, occupiedHexes);
    }

    private async Task MoveUnit(IPlayer player, IUnit unit, MovementType movementType, List<PathSegment> path)
    {
        var command = new MoveUnitCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = player.Id,
            UnitId = unit.Id,
            MovementType = movementType,
            MovementPath = path.Select(s => s.ToData()).ToList()
        };

        await _clientGame.MoveUnit(command);
    }

    private async Task SkipTurn(IPlayer player)
    {
        // Find any unit that hasn't moved yet (fallback)
        var unmovedUnit = player.AliveUnits.FirstOrDefault(u => !u.HasMoved);
        if (unmovedUnit == null)
        {
            throw new BotDecisionException(
                $"No unmoved units available for player {player.Name}",
                nameof(MovementEngine),
                player.Id);
        }

        // Send a StandingStill movement command
        await MoveUnit(player, unmovedUnit, MovementType.StandingStill, []);
    }
}


