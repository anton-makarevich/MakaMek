using Sanet.MakaMek.Map.Models;
using Sanet.Transport.SignalR.Client.Relay;

namespace Sanet.MakaMek.Core.Models.Game;

public interface IGameManager : IDisposable, IAsyncDisposable
{ 
    /// <summary>
    /// Initializes a local-only lobby (creates the ServerGame and logging)
    /// without starting any network transport.
    /// Returns false if the relay room could not be locked or initialization was cancelled.
    /// </summary>
    Task<bool> InitializeLocalLobby(CancellationToken cancellationToken = default);

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
    /// Attempts to transition the server game out of the Start phase. This is the
    /// explicit trigger for starting the game; <see cref="SetBattleMap"/> only
    /// broadcasts the map and never advances the phase on its own.
    /// </summary>
    void TryStartGame();
    
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
    /// Locks the online relay room, if one is currently active. Returns true if lock
    /// succeeded or no room was active; false if lock failed or was cancelled. When false,
    /// state is not cleared allowing the caller to retry.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the lock call.</param>
    /// <returns>True if lock succeeded or no room was active; false if lock failed or was cancelled.</returns>
    Task<bool> LockOnlineRoom(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops any running network transport (online relay and/or LAN host) without
    /// disposing the local ServerGame. Safe to call multiple times and when nothing
    /// is running.
    /// </summary>
    Task StopHosting();
}