using Sanet.MakaMek.Bots.Exceptions;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
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
            // Find unmoved units
            var unmovedUnit = player.AliveUnits.FirstOrDefault(u => u is { HasMoved: false, IsImmobile: false });
            if (unmovedUnit == null)
            {
                // No units to move, skip turn
                await SkipTurn(player);
                return;
            }

            // Handle prone mechs - try to stand up
            if (unmovedUnit is Mech { IsProne: true } mech && mech.CanStandup())
            {
                await AttemptStandup(player, mech);
                return;
            }

            // Select movement type (prefer Walk for Easy difficulty)
            var movementType = SelectMovementType(unmovedUnit);

            // Get a random valid destination
            var destination = GetRandomDestination(unmovedUnit, movementType);
            if (destination == null)
            {
                // No valid destination, just stand still
                await MoveUnit(player, unmovedUnit, MovementType.StandingStill, []);
                return;
            }

            // Find path to destination
            var path = FindPath(unmovedUnit, destination, movementType);
            if (path == null || path.Count == 0)
            {
                // No valid path, just stand still
                await MoveUnit(player, unmovedUnit, MovementType.StandingStill, []);
                return;
            }

            // Move the unit
            await MoveUnit(player, unmovedUnit, movementType, path);
        }
        catch
        {
            // If anything fails, skip turn to avoid blocking the game
            await SkipTurn(player);
        }
    }

    private async Task AttemptStandup(IPlayer player, Mech mech)
    {
        // Select random facing direction
        var directions = new[]
        {
            HexDirection.Top,
            HexDirection.TopRight,
            HexDirection.BottomRight,
            HexDirection.Bottom,
            HexDirection.BottomLeft,
            HexDirection.TopLeft
        };
        var newFacing = directions[Random.Shared.Next(directions.Length)];

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

    private HexPosition? GetRandomDestination(IUnit unit, MovementType movementType)
    {
        if (_clientGame.BattleMap == null || unit.Position == null)
            return null;

        var movementPoints = unit.GetMovementPoints(movementType);
        if (movementPoints <= 0)
            return null;

        // Get occupied hexes (all units except the moving unit)
        var occupiedHexes = _clientGame.Players
            .SelectMany(p => p.Units)
            .Where(u => u is { IsDeployed: true, Position: not null } && u.Id != unit.Id)
            .Select(u => u.Position!.Coordinates)
            .ToList();

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
        var directions = new[]
        {
            HexDirection.Top,
            HexDirection.TopRight,
            HexDirection.BottomRight,
            HexDirection.Bottom,
            HexDirection.BottomLeft,
            HexDirection.TopLeft
        };
        var targetFacing = directions[Random.Shared.Next(directions.Length)];

        return new HexPosition(targetCoordinates, targetFacing);
    }

    private List<PathSegment>? FindPath(IUnit unit, HexPosition destination, MovementType movementType)
    {
        if (_clientGame.BattleMap == null || unit.Position == null)
            return null;

        var movementPoints = unit.GetMovementPoints(movementType);

        // Get occupied hexes (all units except the moving unit)
        var occupiedHexes = _clientGame.Players
            .SelectMany(p => p.Units)
            .Where(u => u is { IsDeployed: true, Position: not null } && u.Id != unit.Id)
            .Select(u => u.Position!.Coordinates)
            .ToList();

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
        // Find any unit that hasn't moved yet
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


