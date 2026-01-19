using System.ComponentModel;
using Sanet.MakaMek.Core.Models.Map;
using ModelContextProtocol.Server;
using MakaMek.Tools.BotContainer.Services;

namespace MakaMek.Tools.BotContainer.Mcp.Tools;

[McpServerToolType]
public class DeploymentTools
{
    private readonly IGameStateProvider _gameStateProvider;

    public DeploymentTools(IGameStateProvider gameStateProvider)
    {
        _gameStateProvider = gameStateProvider;
    }

    [McpServerTool, Description("Get valid deployment hexes for the current game.")]
    public List<HexCoordinates> GetDeploymentZones()
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

        var edgeHexes = map.GetEdgeHexCoordinates().ToList();
        
        return edgeHexes;
    }
}
