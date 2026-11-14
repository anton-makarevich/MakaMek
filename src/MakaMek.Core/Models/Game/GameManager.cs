using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Services.Logging.Factories;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Services.Transport.Relay;
using Sanet.Localization;
using Sanet.MakaMek.Map.Models;
using Sanet.Transport;
using Sanet.Transport.SignalR.Client.Factories;
using Sanet.Transport.SignalR.Client.Relay;

namespace Sanet.MakaMek.Core.Models.Game;

/// <summary>
/// Orchestrates the lobby lifecycle. The transport bootstrap lives in
/// <see cref="RelayHostConnector"/> (relay) and <see cref="LanHostController"/> (LAN), and the
/// server game itself is owned by <see cref="ServerGameHost"/>; this class only sequences them and
/// exposes the lobby state.
/// </summary>
public class GameManager : IGameManager
{
    private readonly RelayHostConnector _relay;
    private readonly LanHostController _lan;
    private readonly ServerGameHost _server;
    private bool _isDisposed;
    private readonly ILogger<GameManager> _logger;

    public GameManager(ICommandPublisher commandPublisher,
        IGameFactory gameFactory,
        ILocalizationService localizationService,
        ICommandLoggerFactory commandLoggerFactory,
        ILogger<GameManager> logger,
        INetworkHostService? networkHostService = null,
        IRelayRoomClient? relayRoomClient = null,
        IPublisherFactory? relayPublisherFactory = null,
        IRelayHubConfigurationProvider? relayHubConfigurationProvider = null,
        IOnlineStatusForwarder? onlineStatusForwarder = null)
    {
        _logger = logger;
        _relay = new RelayHostConnector(
            commandPublisher,
            relayRoomClient,
            relayPublisherFactory,
            relayHubConfigurationProvider,
            onlineStatusForwarder,
            logger);
        _lan = new LanHostController(commandPublisher, networkHostService, logger);
        _server = new ServerGameHost(commandPublisher, gameFactory, localizationService, commandLoggerFactory);
    }

    public async Task ResetForNewGame()
    {
        // Stop forwarding online status before tearing down any publishers
        _relay.ResetForwarding();

        // Remove and dispose any stale relay publisher before re-hosting
        await _relay.RemovePublisherAsync();

        // Remove LAN publisher and stop host service
        await _lan.StopAsync();

        // Dispose current server game if exists
        await _server.DisposeGameAsync();
    }

    public async Task<bool> InitializeLocalLobby(CancellationToken cancellationToken = default)
    {
        // Lock an active relay room before resetting, so the room stops accepting
        // joins. On failure keep the publisher and session state intact so a
        // subsequent cleanup attempt can retry the lock.
        if (!await LockOnlineRoom(cancellationToken))
        {
            _logger.LogWarning(
                "Deferred local lobby initialization: relay room {RoomCode} could not be locked",
                RoomCode);
            return false;
        }

        await ResetForNewGame();
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        _server.Create();
        return true;
    }

    public async Task InitializeLobby(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Reset before initializing new lobby
        await ResetForNewGame();
        cancellationToken.ThrowIfCancellationRequested();

        await _lan.StartAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        _server.Create();
    }

    public async Task InitializeLobbyOnline(CancellationToken cancellationToken = default)
    {
        _relay.ClearError();

        // Lock the currently active relay room before resetting state
        if (!await TryLockActiveRelayRoom(cancellationToken)) return;

        _relay.ClearRoomState();

        // Reset before initializing new lobby (also clears any stale relay publisher)
        await ResetForNewGame();

        var relayOptions = await _relay.GetOptionsForHostingAsync();
        if (relayOptions is null) return;

        // The server game must exist before the relay room is created so the host
        // game id can be reported to the Hub and shared with joiners.
        _server.Create();

        bool hosted;
        try
        {
            hosted = await _relay.HostAsync(_server.GameId!.Value, relayOptions, cancellationToken);
        }
        catch
        {
            // The connector already cleaned up the partial relay session; dispose the
            // server game and its command logging before letting the cancellation escape.
            _server.DisposeGame();
            throw;
        }

        if (hosted) return;

        // The relay publisher is already cleaned up by the connector; dispose the
        // server game and its command logging the same way the failure path does.
        await _relay.CleanupAsync();
        _server.DisposeGame();
    }

    /// <summary>
    /// Locks the room of a previous hosting session so it stops accepting joins. A failed lock
    /// keeps the publisher and session state intact so a later cleanup can retry it.
    /// </summary>
    private async Task<bool> TryLockActiveRelayRoom(CancellationToken cancellationToken)
    {
        if (await LockOnlineRoom(cancellationToken)) return true;

        _relay.FailToLockActiveRoom();
        return false;
    }

    public Task<bool> LockOnlineRoom(CancellationToken cancellationToken = default) =>
        _relay.LockAsync(cancellationToken);

    public async Task StopHosting()
    {
        bool lockSucceeded;
        try
        {
            lockSucceeded = await LockOnlineRoom();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to lock online room during StopHosting");
            lockSucceeded = false;
        }

        if (!lockSucceeded)
        {
            // Keep the retryable relay state (publisher, room code, session token)
            // intact so a subsequent cleanup attempt can retry locking the room.
            _logger.LogWarning(
                "Deferred hosting shutdown: relay room {RoomCode} could not be locked",
                RoomCode);
            return;
        }

        try
        {
            await _relay.RemovePublisherAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to remove online publisher during StopHosting");
        }

        // Reset the session-scoped status stream once hosting is torn down
        _relay.ResetForwarding();

        await _lan.StopAsync();
    }

    public void SetBattleMap(BattleMap battleMap) => _server.SetBattleMap(battleMap);

    public void TryStartGame() => _server.TryStartGame();

    /// <inheritdoc />
    public bool IsGameStarted => _server.IsStarted;

    public string? GetLanServerAddress()
    {
        // Return address only if the host service is actually running
        return !IsLanServerRunning ? null : _lan.HubUrl;
    }
    
    public bool IsLanServerRunning => _lan.IsRunning;
    public bool CanStartLanServer => _lan.CanStart;
    public Guid? ServerGameId => _server.GameId;

    public string? RoomCode => _relay.RoomCode;
    public bool IsOnlineServerRunning => _relay.IsActive && _server.HasGame;
    public RelayClientError? OnlineError => _relay.Error;

    /// <inheritdoc />
    public IObservable<ConnectionStatus> OnlineConnectionStatus => _relay.ConnectionStatus;

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        // Stop forwarding online status
        _relay.ResetForwarding();

        // Dispose server game if it exists
        _server.DisposeGameOnly();

        // Remove LAN publisher from the adapter
        _lan.RemovePublisher();

        // Dispose network host
        _lan.DisposeHost();

        _server.DisposeLogging();
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        // Stop forwarding online status
        _relay.ResetForwarding();

        // Dispose server game if it exists
        _server.DisposeGameOnly();

        // Dispose network host
        await _lan.DisposeHostAsync();

        // Lock the online relay room, if any, before tearing down the publisher
        await LockOnlineRoom();

        // Remove LAN publisher from the adapter
        _lan.RemovePublisher();

        // Remove and dispose online relay publisher if it exists
        await _relay.RemovePublisherAsync();

        _server.DisposeLogging();

        GC.SuppressFinalize(this);
    }
}
