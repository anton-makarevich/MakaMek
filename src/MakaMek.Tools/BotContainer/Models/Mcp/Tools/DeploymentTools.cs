using System.ComponentModel;
using ModelContextProtocol.Server;
using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Tools.BotContainer.Services;

namespace Sanet.MakaMek.Tools.BotContainer.Models.Mcp.Tools;

[McpServerToolType]
public class DeploymentTools
{
    private readonly IGameStateProvider _gameStateProvider;

    public DeploymentTools(IGameStateProvider gameStateProvider)
    {
        _gameStateProvider = gameStateProvider;
    }

    [McpServerTool, Description("Get valid deployment zones (hexes), should be used by the deployment agent")]
    public List<HexCoordinateData> GetDeploymentZones()
    {
        if (_gameStateProvider.ClientGame == null)
        {
            throw new InvalidOperationException("Game is not initialized.");
        }

        var game = _gameStateProvider.ClientGame;
        var map = game.BattleMap;
        
        if (map == null)
        {
             throw new InvalidOperationException("BattleMap is not available.");
        }

        var edgeHexes = map.GetEdgeHexCoordinates()
            .Select(h=>h.ToData())
            .ToList();
        
        return edgeHexes;
    }
}
