using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Bots.DecisionEngines;

/// <summary>
/// Decision engine for the movement phase
/// </summary>
public class MovementEngine : IBotDecisionEngine
{
    private readonly ClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly BotDifficulty _difficulty;
    private readonly Random _random = new();

    public MovementEngine(ClientGame clientGame, IPlayer player, BotDifficulty difficulty)
    {
        _clientGame = clientGame;
        _player = player;
        _difficulty = difficulty;
    }

    public async Task MakeDecision()
    {
        try
        {
            // Find unmoved units
            var unmovedUnit = _player.AliveUnits.FirstOrDefault(u => u is { HasMoved: false, IsImmobile: false });
            if (unmovedUnit == null)
            {
                // No units to move, skip turn
                await SkipTurn();
                return;
            }

            // Handle prone mechs - try to stand up
            if (unmovedUnit is Mech mech && mech.IsProne && mech.CanStandup())
            {
                await AttemptStandup(mech);
                return;
            }

            // Select movement type (prefer Walk for Easy difficulty)
            var movementType = SelectMovementType(unmovedUnit);

            // Get a random valid destination
            var destination = GetRandomDestination(unmovedUnit, movementType);
            if (destination == null)
            {
                // No valid destination, just stand still
                await MoveUnit(unmovedUnit, movementType, []);
                return;
            }

            // Find path to destination
            var path = FindPath(unmovedUnit, destination, movementType);
            if (path == null || path.Count == 0)
            {
                // No valid path, just stand still
                await MoveUnit(unmovedUnit, movementType, []);
                return;
            }

            // Move the unit
            await MoveUnit(unmovedUnit, movementType, path);
        }
        catch
        {
            // If anything fails, skip turn to avoid blocking the game
            await SkipTurn();
        }
    }

    private async Task AttemptStandup(Mech mech)
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
        var newFacing = directions[_random.Next(directions.Length)];

        var command = new TryStandupCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = _player.Id,
            UnitId = mech.Id,
            NewFacing = newFacing,
            MovementTypeAfterStandup = MovementType.Walk
        };

        await _clientGame.TryStandupUnit(command);
    }

    private MovementType SelectMovementType(Unit unit)
    {
        // For Easy difficulty, prefer Walk
        // 80% Walk, 20% Run
        var walkChance = 0.8;

        if (_random.NextDouble() < walkChance)
        {
            return MovementType.Walk;
        }

        // Check if unit can run
        if (unit.GetMovementPoints(MovementType.Run) > 0)
        {
            return MovementType.Run;
        }

        return MovementType.Walk;
    }

    private HexPosition? GetRandomDestination(Unit unit, MovementType movementType)
    {
        if (_clientGame.BattleMap == null || unit.Position == null)
            return null;

        var movementPoints = unit.GetMovementPoints(movementType);
        if (movementPoints <= 0)
            return null;

        // Get occupied hexes
        var occupiedHexes = _clientGame.Players
            .SelectMany(p => p.Units)
            .Where(u => u.IsDeployed && u.Position != null && u.Id != unit.Id)
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
        var targetCoordinates = reachableHexes[_random.Next(reachableHexes.Count)];

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
        var targetFacing = directions[_random.Next(directions.Length)];

        return new HexPosition(targetCoordinates, targetFacing);
    }

    private List<PathSegment>? FindPath(Unit unit, HexPosition destination, MovementType movementType)
    {
        if (_clientGame.BattleMap == null || unit.Position == null)
            return null;

        var movementPoints = unit.GetMovementPoints(movementType);

        // Get occupied hexes
        var occupiedHexes = _clientGame.Players
            .SelectMany(p => p.Units)
            .Where(u => u.IsDeployed && u.Position != null && u.Id != unit.Id)
            .Select(u => u.Position!.Coordinates)
            .ToList();

        return _clientGame.BattleMap.FindPath(unit.Position, destination, movementPoints, occupiedHexes);
    }

    private async Task MoveUnit(Unit unit, MovementType movementType, List<PathSegment> path)
    {
        var command = new MoveUnitCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = _player.Id,
            UnitId = unit.Id,
            MovementType = movementType,
            MovementPath = path.Select(s => s.ToData()).ToList()
        };

        await _clientGame.MoveUnit(command);
    }

    private async Task SkipTurn()
    {
        // Movement phase doesn't require explicit turn ending
        // The phase will automatically progress when all units have moved
        await Task.CompletedTask;
    }
}

