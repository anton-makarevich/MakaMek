using Sanet.MakaMek.Core.Services.Transport.Relay;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game;

public interface IGameManager : IDisposable, IAsyncDisposable
{ 
    /// <summary>
    /// Initializes the lobby asynchronously
    /// </summary>
    Task InitializeLobby();

    /// <summary>
    /// Initializes the lobby hosted through the cloud relay asynchronously.
    /// The server game is created first so its id can be reported to the Hub.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the room lifecycle calls.</param>
    Task InitializeLobbyOnline(
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

    /// <summary>
    /// Closes the online relay room, if one is currently active. Best-effort and idempotent:
    /// failures are swallowed and calling this when no online room is active is a no-op.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the close call.</param>
    Task CloseOnlineRoom(CancellationToken cancellationToken = default);
}