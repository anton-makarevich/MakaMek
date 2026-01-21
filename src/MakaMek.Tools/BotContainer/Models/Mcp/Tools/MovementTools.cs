using System.ComponentModel;
using MakaMek.Tools.BotContainer.Models.Data.Mcp;
using MakaMek.Tools.BotContainer.Services;
using ModelContextProtocol.Server;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;

namespace MakaMek.Tools.BotContainer.Models.Mcp.Tools;

[McpServerToolType]
public class MovementTools
{
    private readonly IGameStateProvider _gameStateProvider;

    public MovementTools(IGameStateProvider gameStateProvider)
    {
        _gameStateProvider = gameStateProvider;
    }

    [McpServerTool, Description("Get all reachable hexes for a unit, grouped by coordinates with tactical evaluation")]
    public async Task<List<ReachableHexData>> GetReachableHexes(Guid unitId)
    {
        if (_gameStateProvider.ClientGame == null)
            throw new InvalidOperationException("Game is not initialized.");
        if (_gameStateProvider.TacticalEvaluator == null)
            throw new InvalidOperationException("TacticalEvaluator is not available.");

        var game = _gameStateProvider.ClientGame;
        var map = game.BattleMap;
        if (map == null)
            throw new InvalidOperationException("BattleMap is not available.");
        
        // Find player and unit
        var player = game.Players.FirstOrDefault(p => p.Units.Any(u => u.Id == unitId));
        var unit = player?.Units.FirstOrDefault(u => u.Id == unitId);
        
        if (unit?.Position == null)
            throw new InvalidOperationException($"Unit {unitId} not found or not on map.");

        // 1. Get occupied hexes (exclude moving unit)
        var occupiedHexes = game.Players
            .SelectMany(p => p.Units)
            .Where(u => u is { IsDeployed: true } && u.Id != unitId)
            .Select(u => u.Position!.Coordinates)
            .ToHashSet();

        var friendlyPositions = player!.AliveUnits
            .Where(u => u is { IsDeployed: true })
            .Select(u => u.Position!.Coordinates)
            .ToHashSet();

        var enemyUnits = game.Players
            .Where(p => p.Id != player.Id)
            .SelectMany(p => p.AliveUnits)
            .Where(u => u is { IsDeployed: true })
            .ToList();

        // 2. Identify available movement types
        var availableMovementTypes = new List<MovementType> { MovementType.Walk };
        // TODO: to be refactored, see #705
        if (unit is Sanet.MakaMek.Core.Models.Units.Mechs.Mech mech)
        {
            if (mech.CanRun) availableMovementTypes.Add(MovementType.Run);
            if (mech.CanJump) availableMovementTypes.Add(MovementType.Jump);
        }
        else if (unit.GetMovementPoints(MovementType.Run) > 0)
        {
            availableMovementTypes.Add(MovementType.Run);
        }

        // 3. Calculate reachable hexes and evaluate options
        var hexOptions = new Dictionary<HexCoordinateData, List<MovementOption>>();

        foreach (var moveType in availableMovementTypes)
        {
            var reachabilityData = map.GetReachableHexesForUnit(
                unit,
                moveType,
                occupiedHexes,
                friendlyPositions);

            foreach (var hex in reachabilityData.AllReachableHexes)
            {
                // Let's get the path to this hex
                var paths = map.GetPathsToHexWithAllFacings(
                    unit.Position,
                    hex,
                    moveType,
                    unit.GetMovementPoints(moveType),
                    reachabilityData,
                    occupiedHexes);

                // Evaluate each valid path (each facing)
                foreach (var path in paths.Values)
                {
                    try
                    {
                        var score = await _gameStateProvider.TacticalEvaluator.EvaluatePath(unit, path, enemyUnits);

                        var option = new MovementOption(
                            moveType.ToString(),
                            score.OffensiveIndex,
                            score.DefensiveIndex,
                            (int)path.Destination.Facing
                        );

                        var coordData = hex.ToData();
                        if (!hexOptions.ContainsKey(coordData))
                        {
                            hexOptions[coordData] = [];
                        }

                        hexOptions[coordData].Add(option);

                    }
                    catch (Exception ex)
                    {
                        _gameStateProvider.ClientGame.Logger.LogError(ex, "Failed to evaluate path to {Position}: {Message}", hex, ex.Message);
                         // Ignore invalid paths
                    }
                }
            }
        }

        return hexOptions.Select(kv => new ReachableHexData(kv.Key.Q, kv.Key.R, kv.Value)).ToList();
    }

    [McpServerTool, Description("Get path segments for a specific move command")]
    public IReadOnlyList<PathSegmentData> GetMovementPath(
        Guid unitId, 
        int q, 
        int r, 
        string movementTypeStr, 
        int facing)
    {
        if (_gameStateProvider.ClientGame == null)
            throw new InvalidOperationException("Game is not initialized.");

        var game = _gameStateProvider.ClientGame;
        var map = game.BattleMap;
        if (map == null) throw new InvalidOperationException("BattleMap is not available.");

        var player = game.Players.FirstOrDefault(p => p.Units.Any(u => u.Id == unitId));
        var unit = player?.Units.FirstOrDefault(u => u.Id == unitId);
        if (unit == null || unit.Position == null)
            throw new InvalidOperationException($"Unit {unitId} not found.");

        if (!Enum.TryParse<MovementType>(movementTypeStr, true, out var movementType))
            throw new ArgumentException($"Invalid movement type: {movementTypeStr}");

        if (!Enum.IsDefined(typeof(HexDirection), facing))
             throw new ArgumentException($"Invalid facing: {facing}");
        var targetFacing = (HexDirection)facing;

        var targetHex = map.GetHex(new HexCoordinates(q, r));
        if (targetHex == null) throw new ArgumentException($"Invalid coordinates: {q},{r}");

        // Re-calculate reachability to be safe/consistent
        var occupiedHexes = game.Players
             .SelectMany(p => p.Units)
             .Where(u => u is { IsDeployed: true, Position: not null } && u.Id != unitId)
             .Select(u => u.Position!.Coordinates)
             .ToHashSet();

        var friendlyPositions = player!.AliveUnits
            .Where(u => u is { IsDeployed: true, Position: not null })
            .Select(u => u.Position!.Coordinates)
            .ToHashSet();

        var reachabilityData = map.GetReachableHexesForUnit(
                unit,
                movementType,
                occupiedHexes,
                friendlyPositions);

        if (!reachabilityData.IsHexReachable(targetHex.Coordinates))
             throw new InvalidOperationException("Target hex is not reachable.");

        var paths = map.GetPathsToHexWithAllFacings(
                    unit.Position,
                    targetHex.Coordinates,
                    movementType,
                    unit.GetMovementPoints(movementType),
                    reachabilityData,
                    occupiedHexes);

        // Find the path that ends with the requested facing
        var matchingPath = paths.Values.FirstOrDefault(p => p.Destination.Facing == targetFacing);
        
        if (matchingPath == null)
            throw new InvalidOperationException($"Cannot reach target hex {q},{r} with facing {targetFacing} using {movementType}.");

        return matchingPath.ToData();
    }
}