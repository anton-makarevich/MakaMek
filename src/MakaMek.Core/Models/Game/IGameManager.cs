using Sanet.MakaMek.Core.Services.Transport.Relay;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game;

public interface IGameManager : IDisposable
{ 
    /// <summary>
    /// Initializes the lobby asynchronously
    /// </summary>
    Task InitializeLobby();

    /// <summary>
    /// Initializes the lobby hosted through the cloud relay asynchronously.
    /// </summary>
    /// <param name="playerId">Id of the player hosting the lobby.</param>
    /// <param name="playerName">Name of the player hosting the lobby.</param>
    /// <param name="cancellationToken">Cancellation token for the room lifecycle calls.</param>
    Task InitializeLobbyOnline(
        Guid playerId,
        string playerName,
        CancellationToken cancellationToken = default);
    
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

    /// <summary>
    /// Gets the room code of the online lobby, or null when no online lobby is running.
    /// </summary>
    string? RoomCode { get; }

    /// <summary>
    /// Gets a value indicating whether the online (relay) server is running.
    /// </summary>
    bool IsOnlineServerRunning { get; }

    /// <summary>
    /// Gets the last error reported by the online hosting flow, if any.
    /// </summary>
    RelayClientError? OnlineError { get; }
}