using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game;

public interface IGameManager : IDisposable
{ 
    /// <summary>
    /// Initializes the lobby asynchronously
    /// </summary>
    Task InitializeLobby();
    
    /// <summary>
    /// Sets the battle map for the game
    /// </summary>
    /// <param name="battleMap">The battle map to use</param>
    void SetBattleMap(BattleMap battleMap);
    
    /// <summary>
    /// Gets the LAN server address for clients to connect to
    /// </summary>
    /// <returns>The server IP address</returns>
    string? GetLanServerAddress();
    
    /// <summary>
    /// Gets a value indicating whether the LAN server is running
    /// </summary>
    bool IsLanServerRunning { get; }
    
    /// <summary>
    /// Gets a value indicating whether the LAN server can be started
    /// </summary>
    bool CanStartLanServer { get; }

    Guid? ServerGameId { get; }
}