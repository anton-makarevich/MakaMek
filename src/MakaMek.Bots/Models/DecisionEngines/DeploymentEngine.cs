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
            var undeployedUnits = player.Units.Where(u => !u.IsDeployed).ToList();
            if (undeployedUnits.Count == 0)
            {
                // No units to deploy, skip turn
                return;
            }

            // 2. Select first undeployed unit (simple strategy)
            var unit = undeployedUnits.First();

            // 3. Get valid deployment hexes from map
            var validHexes = GetValidDeploymentHexes();
            if (validHexes.Count == 0)
            {
                // No valid deployment hexes available
                return;
            }

            // 4. Select random hex and direction
            var selectedHex = validHexes[Random.Shared.Next(validHexes.Count)];
            var selectedDirection = (HexDirection)Random.Shared.Next(0, 6);

            // 5. Create and send deploy command
            var deployCommand = new DeployUnitCommand
            {
                GameOriginId = _clientGame.Id,
                PlayerId = player.Id,
                UnitId = unit.Id,
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

    private List<HexCoordinates> GetValidDeploymentHexes()
    {
        var validHexes = new List<HexCoordinates>();

        if (_clientGame.BattleMap == null)
            return validHexes;

        // For Phase 1, use simple deployment strategy:
        // Deploy on the first two rows of the map (basic deployment zone)
        for (int q = 1; q <= _clientGame.BattleMap.Width; q++)
        {
            for (int r = 1; r <= Math.Min(2, _clientGame.BattleMap.Height); r++)
            {
                var coordinates = new HexCoordinates(q, r);
                var hex = _clientGame.BattleMap.GetHex(coordinates);

                // Check if hex exists and is not occupied
                if (hex != null && !IsHexOccupied(coordinates))
                {
                    validHexes.Add(coordinates);
                }
            }
        }

        return validHexes;
    }

    private bool IsHexOccupied(HexCoordinates coordinates)
    {
        // Check if any unit is already deployed at this position
        return _clientGame.Players
            .SelectMany(p => p.Units)
            .Any(u => u.Position?.Coordinates == coordinates);
    }
}

