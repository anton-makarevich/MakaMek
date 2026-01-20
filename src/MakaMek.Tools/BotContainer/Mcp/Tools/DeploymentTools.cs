using System.ComponentModel;
using Sanet.MakaMek.Core.Models.Map;
using ModelContextProtocol.Server;
using MakaMek.Tools.BotContainer.Services;
using Sanet.MakaMek.Core.Data.Map;

namespace MakaMek.Tools.BotContainer.Mcp.Tools;

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
