using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Bots.Models.DecisionEngines;

/// <summary>
/// Decision engine for the deployment phase
/// </summary>
public class DeploymentEngine : IBotDecisionEngine
{
    private readonly IClientGame _clientGame;

    public DeploymentEngine(IClientGame clientGame)
    {
        _clientGame = clientGame;
    }

    public async Task MakeDecision(IPlayer player)
    {
        try
        {
            // 1. Find undeployed units
            var undeployedUnit = player.Units.FirstOrDefault(u => !u.IsDeployed);
            if (undeployedUnit == null)
            {
                // No units to deploy, skip turn
                return;
            }

            // 2. Get occupied hexes and enemy units (cached for efficiency)
            var occupiedHexes = _clientGame.Players
                .SelectMany(p => p.Units)
                .Where(u => u is { IsDeployed: true, Position: not null })
                .Select(u => u.Position!.Coordinates)
                .ToHashSet();

            var deployedEnemyUnits = _clientGame.Players
                .Where(p => p.Id != player.Id)
                .SelectMany(p => p.Units)
                .Where(u => u is { IsDeployed: true, Position: not null })
                .ToList();

            // 3. Get valid deployment hexes from map
            var validHexes = GetValidDeploymentHexes(occupiedHexes);
            if (validHexes.Count == 0)
            {
                // No valid deployment hexes available
                return;
            }

            // 4. Select random hex and calculate strategic direction
            var selectedHex = validHexes[Random.Shared.Next(validHexes.Count)];
            var selectedDirection = GetDeploymentDirection(selectedHex, deployedEnemyUnits);

            // 5. Create and send deploy command
            var deployCommand = new DeployUnitCommand
            {
                GameOriginId = _clientGame.Id,
                PlayerId = player.Id,
                UnitId = undeployedUnit.Id,
                Position = selectedHex.ToData(),
                Direction = (int)selectedDirection
            };

            await _clientGame.DeployUnit(deployCommand);
        }
        catch (Exception ex)
        {
            // Log error but don't throw - graceful degradation
            Console.WriteLine($"DeploymentEngine error for player {player.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the deployment area (edges of the map). Can be overridden for custom deployment zones.
    /// </summary>
    protected virtual HashSet<HexCoordinates> GetDeploymentArea()
    {
        var deploymentArea = new HashSet<HexCoordinates>();

        if (_clientGame.BattleMap == null)
            return deploymentArea;

        var width = _clientGame.BattleMap.Width;
        var height = _clientGame.BattleMap.Height;

        // Add first and last rows
        for (var q = 1; q <= width; q++)
        {
            deploymentArea.Add(new HexCoordinates(q, 1));
            deploymentArea.Add(new HexCoordinates(q, height));
        }

        // Add first and last columns (excluding corners already added)
        for (var r = 2; r < height; r++)
        {
            deploymentArea.Add(new HexCoordinates(1, r));
            deploymentArea.Add(new HexCoordinates(width, r));
        }

        return deploymentArea;
    }

    private List<HexCoordinates> GetValidDeploymentHexes(HashSet<HexCoordinates> occupiedHexes)
    {
        if (_clientGame.BattleMap == null)
            return [];

        // Get deployment area (edges of the map)
        var deploymentArea = GetDeploymentArea();

        // Return unoccupied hexes in deployment area
        return deploymentArea
            .Where(hex => !occupiedHexes.Contains(hex))
            .ToList();
    }

    private HexDirection GetDeploymentDirection(HexCoordinates deployPosition, List<IUnit> deployedEnemyUnits)
    {
        HexCoordinates target;

        // Determine target: nearest enemy or map center
        if (deployedEnemyUnits.Count > 0)
        {
            // Find nearest enemy unit
            var nearestEnemy = deployedEnemyUnits
                .MinBy(enemy => deployPosition.DistanceTo(enemy.Position!.Coordinates));
            target = nearestEnemy!.Position!.Coordinates;
        }
        else
        {
            // Face toward map center
            if (_clientGame.BattleMap == null)
                return HexDirection.Top; // Fallback

            var centerQ = _clientGame.BattleMap.Width / 2;
            var centerR = _clientGame.BattleMap.Height / 2;
            target = new HexCoordinates(centerQ, centerR);
        }

        // If already at target (edge case), default to Top
        if (deployPosition.Equals(target))
        {
            throw new InvalidOperationException(
                $"Cannot deploy at target position {deployPosition}");
        }

        // Get line of sight to target
        var lineSegments = deployPosition.LineTo(target);
        
        // Skip the first segment (current position) and get the first adjacent hex
        if (lineSegments.Count < 2)
        {
            // Should not happen, but fallback to Top
            return HexDirection.Top;
        }

        var firstSegment = lineSegments[1];
        
        // If segment has two options (equal-distance hexes), randomly select one
        var adjacentHex = firstSegment.SecondOption != null && Random.Shared.Next(2) == 0
            ? firstSegment.SecondOption
            : firstSegment.MainOption;

        // Get direction to the adjacent hex
        return deployPosition.GetDirectionToNeighbour(adjacentHex);
    }
}

