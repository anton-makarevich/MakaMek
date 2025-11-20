using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Bots.DecisionEngines;

/// <summary>
/// Decision engine for the movement phase
/// </summary>
public class MovementEngine : IBotDecisionEngine
{
    private readonly IClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly BotDifficulty _difficulty;

    public MovementEngine(IClientGame clientGame, IPlayer player, BotDifficulty difficulty)
    {
        _clientGame = clientGame;
        _player = player;
        _difficulty = difficulty;
    }

    public async Task MakeDecision()
    {
        try
        {
            // 1. Find unmoved units
            var unmovedUnits = _player.AliveUnits.Where(u => u.MovementTypeUsed == null).ToList();
            if (!unmovedUnits.Any())
            {
                // No units to move, skip turn
                return;
            }

            // 2. Select first unmoved unit (simple strategy)
            var unit = unmovedUnits.First();

            if (unit.Position == null || _clientGame.BattleMap == null)
            {
                // Unit not deployed or no map available
                return;
            }

            // 3. Select movement type (prefer Walk for Phase 1)
            var movementType = MovementType.Walk;
            var movementPoints = unit.GetMovementPoints(movementType);

            // 4. Find a random valid destination
            var prohibitedHexes = GetProhibitedHexes();
            var reachableHexes = _clientGame.BattleMap
                .GetReachableHexes(unit.Position, movementPoints, prohibitedHexes)
                .ToList();

            if (!reachableHexes.Any())
            {
                // No reachable hexes, stand still
                await MoveUnit(unit, movementType, []);
                return;
            }

            // 5. Select random destination
            var destination = reachableHexes[Random.Shared.Next(reachableHexes.Count)];
            var targetPosition = new HexPosition(destination.coordinates, unit.Position.Facing);

            // 6. Find path to destination
            var path = _clientGame.BattleMap.FindPath(unit.Position, targetPosition, movementPoints, prohibitedHexes);

            if (path == null)
            {
                // No path found, stand still
                await MoveUnit(unit, movementType, []);
                return;
            }

            // 7. Convert path to command format
            var pathData = path.Select(segment => segment.ToData()).ToList();
            await MoveUnit(unit, movementType, pathData);
        }
        catch (Exception ex)
        {
            // Log error but don't throw - graceful degradation
            Console.WriteLine($"MovementEngine error for player {_player.Name}: {ex.Message}");
        }
    }

    private async Task MoveUnit(Unit unit, MovementType movementType, List<PathSegmentData> path)
    {
        var moveCommand = new MoveUnitCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = _player.Id,
            UnitId = unit.Id,
            MovementType = movementType,
            MovementPath = path
        };

        await _clientGame.MoveUnit(moveCommand);
    }

    private List<HexCoordinates> GetProhibitedHexes()
    {
        // Get hexes with enemy units - these will be excluded from pathfinding
        return _clientGame.Players
            .Where(p => p.Id != _player.Id)
            .SelectMany(p => p.AliveUnits)
            .Where(u => u.Position != null)
            .Select(u => u.Position!.Coordinates)
            .ToList();
    }
}

