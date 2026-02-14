using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Bots.Exceptions;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

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

    public async Task MakeDecision(IPlayer player, ITurnState? turnState = null)
    {
        try
        {
            // 1. Find undeployed units
            var undeployedUnit = player.Units.FirstOrDefault(u => !u.IsDeployed);
            if (undeployedUnit == null)
            {
                throw new BotDecisionException(
                    $"No undeployed units available for player {player.Name}",
                    nameof(DeploymentEngine),
                    player.Id);
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
                throw new BotDecisionException(
                    "No valid deployment hexes available on the map",
                    nameof(DeploymentEngine),
                    player.Id);
            }

            // 4. Select random hex and calculate strategic direction
            var selectedHex = validHexes[Random.Shared.Next(validHexes.Count)];
            var selectedDirection = GetDeploymentDirection(selectedHex, deployedEnemyUnits, player);

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
        catch (BotDecisionException ex)
        {
            // Rethrow BotDecisionException to let caller handle decision failures
            _clientGame.Logger.LogError(ex, "DeploymentEngine error for player {PlayerName}: {Message}", player.Name, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            // Log error but don't throw - graceful degradation for unexpected errors
            _clientGame.Logger.LogError(ex, "DeploymentEngine error for player {PlayerName}: {Message}", player.Name, ex.Message);
        }
    }

    /// <summary>
    /// Gets the deployment area (edges of the map). Can be overridden for custom deployment zones.
    /// </summary>
    private HashSet<HexCoordinates> GetDeploymentArea()
    {
        return _clientGame.BattleMap == null ? [] :
            // Use extension method and convert to HashSet for efficient lookups
            _clientGame.BattleMap.GetEdgeHexCoordinates().ToHashSet();
    }

    private List<HexCoordinates> GetValidDeploymentHexes(HashSet<HexCoordinates> occupiedHexes)
    {
        // Get deployment area (edges of the map)
        var deploymentArea = GetDeploymentArea();

        // Return unoccupied hexes in deployment area
        return deploymentArea
            .Where(hex => !occupiedHexes.Contains(hex))
            .ToList();
    }

    private HexDirection GetDeploymentDirection(HexCoordinates deployPosition, List<IUnit> deployedEnemyUnits, IPlayer player)
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
            target = _clientGame.BattleMap!.GetCenterHexCoordinate();
        }

        // If already at target (edge case, for unrealistic one hex maps), throw exception
        if (deployPosition.Equals(target))
        {
            throw new BotDecisionException(
                $"Cannot deploy at target position {deployPosition}",
                nameof(DeploymentEngine),
                player.Id);
        }

        // Get line of sight to target
        var lineSegments = deployPosition.LineTo(target);

        var firstSegment = lineSegments[1];
        
        // If segment has two options (equal-distance hexes), randomly select one
        var adjacentHex = firstSegment.SecondOption != null && Random.Shared.Next(2) == 0
            ? firstSegment.SecondOption
            : firstSegment.MainOption;

        // Get direction to the adjacent hex
        return deployPosition.GetDirectionToNeighbour(adjacentHex);
    }
}

