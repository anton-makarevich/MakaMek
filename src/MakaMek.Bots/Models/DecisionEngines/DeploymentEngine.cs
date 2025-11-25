using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;

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

            // 2. Get valid deployment hexes from map
            var validHexes = GetValidDeploymentHexes();
            if (validHexes.Count == 0)
            {
                // No valid deployment hexes available
                return;
            }

            // 3. Select random hex and direction
            var selectedHex = validHexes[Random.Shared.Next(validHexes.Count)];
            var selectedDirection = GetRandomDirection();

            // 4. Create and send deploy command
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

    private List<HexCoordinates> GetValidDeploymentHexes()
    {
        if (_clientGame.BattleMap == null)
            return [];

        // Get deployment area (edges of the map)
        var deploymentArea = GetDeploymentArea();

        // Get occupied hexes using HashSet for efficient lookups
        var occupiedHexes = _clientGame.Players
            .SelectMany(p => p.Units)
            .Where(u => u is { IsDeployed: true, Position: not null })
            .Select(u => u.Position!.Coordinates)
            .ToHashSet();

        // Return unoccupied hexes in deployment area
        return deploymentArea
            .Where(hex => !occupiedHexes.Contains(hex))
            .ToList();
    }

    private static HexDirection GetRandomDirection()
    {
        var directions = new[]
        {
            HexDirection.Top,
            HexDirection.TopRight,
            HexDirection.BottomRight,
            HexDirection.Bottom,
            HexDirection.BottomLeft,
            HexDirection.TopLeft
        };

        return directions[Random.Shared.Next(directions.Length)];
    }
}

