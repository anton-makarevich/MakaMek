using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Services.Logging;
using Sanet.MakaMek.Core.Services.Logging.Factories;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Services.Transport.Relay;
using Sanet.Localization;
using Sanet.MakaMek.Map.Models;
using Sanet.Transport;
using Sanet.Transport.Rx;
using Sanet.Transport.SignalR.Client.Factories;
using Sanet.Transport.SignalR.Client.Relay;

namespace Sanet.MakaMek.Core.Models.Game;

public class GameManager : IGameManager
{
    private readonly ICommandPublisher _commandPublisher;
    private readonly IGameFactory _gameFactory;
    private ServerGame? _serverGame;
    private readonly INetworkHostService? _networkHostService;
    private bool _isDisposed;
    private readonly ILocalizationService _localizationService;
    private readonly ICommandLoggerFactory _commandLoggerFactory;
    private readonly ILogger<GameManager> _logger;
    private ICommandLogger? _commandLogger;
    private Action<IGameCommand>? _logHandler;
    private readonly IRelayRoomClient? _relayRoomClient;
    private readonly IPublisherFactory? _relayPublisherFactory;
    private readonly IRelayHubConfigurationProvider? _relayHubConfigurationProvider;
    private ITransportPublisher? _onlineRelayPublisher;
    private ITransportPublisher? _lanPublisher;
    private string? _onlineSessionToken;
    private RelayClientOptions? _onlineRelayOptions;
    private readonly IOnlineStatusForwarder? _forwarder;
    private readonly BehaviorSubject<ConnectionStatus> _fallbackOnlineStatus = new(ConnectionStatus.NotConnected);

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
        _commandPublisher = commandPublisher;
        _gameFactory = gameFactory;
        _localizationService = localizationService;
        _commandLoggerFactory = commandLoggerFactory;
        _logger = logger;
        _networkHostService = networkHostService;
        _relayRoomClient = relayRoomClient;
        _relayPublisherFactory = relayPublisherFactory;
        _relayHubConfigurationProvider = relayHubConfigurationProvider;
        _forwarder = onlineStatusForwarder;
    }
    
    private static Action<IGameCommand> SafeLog(ICommandLogger logger) =>
        command =>
        {
            try
            {
                logger.Log(command);
            }
            catch
            {
                // Swallow to avoid impacting a publisher
            }
        };

    public async Task ResetForNewGame()
    {
        // Stop forwarding online status before tearing down any publishers
        _forwarder?.Reset();

        // Remove and dispose any stale relay publisher before re-hosting
        await _onlineRelayPublisher.RemoveAndDisposeAsync(_commandPublisher.Adapter, _logger);
        _onlineRelayPublisher = null;

        // Remove LAN publisher and stop host service
        await RemoveLanPublisherAndStopHost();

        // Dispose current server game if exists
        if (_serverGame != null)
        {
            _serverGame.Dispose();

            // Wait a bit for disposal to complete
            await Task.Delay(200);

            _serverGame = null;
        }

        // Unsubscribe logging handler
        if (_logHandler != null)
        {
            _commandPublisher.Unsubscribe(_logHandler);
            _logHandler = null;
        }

        // Dispose command logger
        _commandLogger?.Dispose();
        _commandLogger = null; 
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
        CreateServerGameAndSetupLogging();
        return true;
    }

    public async Task InitializeLobby(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Reset before initializing new lobby
        await ResetForNewGame();
        cancellationToken.ThrowIfCancellationRequested();

        var transportAdapter = _commandPublisher.Adapter;
        // Start the network host if supported and not already running
        if (CanStartLanServer && !IsLanServerRunning && _networkHostService != null)
        {
            await _networkHostService.Start();

            if (cancellationToken.IsCancellationRequested)
            {
                // The host started after cancellation was requested; stop it
                // again so no orphaned LAN server is left running.
                await RemoveLanPublisherAndStopHost();
                cancellationToken.ThrowIfCancellationRequested();
            }

            // Add the network publisher to the transport adapter if successfully started
            if (_networkHostService.Publisher != null)
            {
                try
                {
                    transportAdapter.AddPublisher(_networkHostService.Publisher);
                    _lanPublisher = _networkHostService.Publisher;

                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException)
                {
                    // Cancellation occurred after the publisher was registered;
                    // remove it and stop the host so nothing is left orphaned.
                    await RemoveLanPublisherAndStopHost();
                    throw;
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        CreateServerGameAndSetupLogging();
    }

    public async Task InitializeLobbyOnline(CancellationToken cancellationToken = default)
    {
        OnlineError = null;

        // Lock the currently active relay room before resetting state
        if (!await TryLockActiveRelayRoom(cancellationToken)) return;

        RoomCode = null;

        // Reset before initializing new lobby (also clears any stale relay publisher)
        await ResetForNewGame();

        var relayOptions = await GetRelayOptionsForHostingAsync();
        if (relayOptions is null) return;

        // The server game must exist before the relay room is created so the host
        // game id can be reported to the Hub and shared with joiners.
        CreateServerGameAndSetupLogging();

        await HostRelayRoomAsync(_serverGame!.Id, relayOptions, cancellationToken);
    }

    /// <summary>
    /// Locks the room of a previous hosting session so it stops accepting joins. A failed lock
    /// keeps the publisher and session state intact so a later cleanup can retry it.
    /// </summary>
    private async Task<bool> TryLockActiveRelayRoom(CancellationToken cancellationToken)
    {
        if (await LockOnlineRoom(cancellationToken)) return true;

        OnlineError = new RelayClientError(
            RelayClientErrorCode.Unknown,
            "Failed to lock the currently active relay room.");
        return false;
    }

    /// <summary>
    /// Reads the hub configuration pinned for the whole room lifecycle. Create, Ready, Lock and
    /// the publisher all target the hub that was selected when hosting began.
    /// </summary>
    private async Task<RelayClientOptions?> GetRelayOptionsForHostingAsync()
    {
        // Wait for persisted hub configuration before reading the active values below.
        var relayOptions = _relayHubConfigurationProvider is null
            ? null
            : await _relayHubConfigurationProvider.GetActiveOptions();

        // Relay hosting requires the room client, the publisher factory, and an active hub configuration
        if (_relayRoomClient is null
            || _relayPublisherFactory is null
            || relayOptions is null
            || string.IsNullOrWhiteSpace(relayOptions.BaseUrl))
        {
            OnlineError = new RelayClientError(
                RelayClientErrorCode.ConfigurationError,
                "Relay hosting is not configured on this platform.");
            return null;
        }

        return relayOptions;
    }

    /// <summary>
    /// Creates the relay room and connects the host publisher to it. The room is disposed when
    /// the relay does not report the identity required to share it with joiners.
    /// </summary>
    private async Task HostRelayRoomAsync(
        Guid gameId,
        RelayClientOptions relayOptions,
        CancellationToken cancellationToken)
    {
        var createResult = await _relayRoomClient!.Create(
            RelayGameInfoFactory.Create(gameId),
            cancellationToken,
            relayOptions);
        if (!createResult.Success
            || createResult.RoomCode is null
            || createResult.SessionToken is null
            || createResult.GameInfo is null)
        {
            OnlineError = createResult.Error
                ?? new RelayClientError(
                    RelayClientErrorCode.Unknown,
                    "The relay did not return the values required to host.");
            _logger.LogWarning(
                "Failed to create relay room for game {GameId}: {ErrorCode} {ErrorMessage}",
                gameId,
                OnlineError?.Code,
                OnlineError?.Message);

            // The server game was created above; dispose it the same way the failure
            // path does after a publisher/ready failure.
            await CleanupOnlineAfterFailure(publisher: null);
            return;
        }

        if (!await ConnectRelayPublisherAsync(
                createResult.RoomCode,
                createResult.SessionToken,
                relayOptions,
                cancellationToken))
            return;

        RoomCode = createResult.RoomCode;
        _onlineSessionToken = createResult.SessionToken;
        _onlineRelayOptions = relayOptions;
        _logger.LogInformation(
            "Hosted relay room {RoomCode} for game {GameId} {GameVersion}; relay publisher connected",
            createResult.RoomCode,
            createResult.GameInfo.HostId,
            createResult.GameInfo.Version);
    }

    /// <summary>
    /// Connects the host publisher to the created room and confirms the room as ready, cleaning up
    /// the partial session on failure or cancellation so nothing is left orphaned. Returns false
    /// when the session was cleaned up instead of completed.
    /// </summary>
    private async Task<bool> ConnectRelayPublisherAsync(
        string roomCode,
        string sessionToken,
        RelayClientOptions relayOptions,
        CancellationToken cancellationToken)
    {
        ITransportPublisher? publisher = null;
        try
        {
            var ticket = await GetRelayTicketAsync(roomCode, sessionToken, relayOptions, cancellationToken);
            if (ticket is null)
            {
                await LockRelayRoomAndCleanup(roomCode, sessionToken, publisher, relayOptions);
                return false;
            }

            publisher = await _relayPublisherFactory!.Create(
                relayOptions.CreatePublisherOptions(roomCode, ticket),
                cancellationToken);

            _commandPublisher.Adapter.AddPublisher(publisher);
            _onlineRelayPublisher = publisher;
            _forwarder?.Start(_commandPublisher.Adapter);

            if (await MarkRelayRoomReadyAsync(roomCode, sessionToken, relayOptions, cancellationToken))
                return true;

            await LockRelayRoomAndCleanup(roomCode, sessionToken, publisher, relayOptions);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await LockRelayRoomAndCleanup(roomCode, sessionToken, publisher, relayOptions);
            throw;
        }
        catch (Exception)
        {
            OnlineError = new RelayClientError(
                RelayClientErrorCode.Unknown,
                "Failed to connect the host to the relay.");
            await LockRelayRoomAndCleanup(roomCode, sessionToken, publisher, relayOptions);
            return false;
        }
    }

    private async Task<string?> GetRelayTicketAsync(
        string roomCode,
        string sessionToken,
        RelayClientOptions relayOptions,
        CancellationToken cancellationToken)
    {
        var ticketResult = await _relayRoomClient!.GetRelayTicket(
            roomCode,
            sessionToken,
            cancellationToken,
            relayOptions);
        if (ticketResult.Success && !string.IsNullOrWhiteSpace(ticketResult.Ticket)) return ticketResult.Ticket;

        OnlineError = ticketResult.Error
            ?? new RelayClientError(
                RelayClientErrorCode.Unknown,
                "The relay did not issue a relay ticket for the host session.");
        return null;
    }

    private async Task<bool> MarkRelayRoomReadyAsync(
        string roomCode,
        string sessionToken,
        RelayClientOptions relayOptions,
        CancellationToken cancellationToken)
    {
        var readyResult = await _relayRoomClient!.Ready(
            roomCode,
            sessionToken,
            cancellationToken,
            relayOptions);
        if (readyResult.Success) return true;

        OnlineError = readyResult.Error
            ?? new RelayClientError(
                RelayClientErrorCode.Unknown,
                "The relay did not confirm the room as ready.");
        return false;
    }

    private void CreateServerGameAndSetupLogging()
    {
        // Create the game server instance
        _serverGame = _gameFactory.CreateServerGame(_commandPublisher);
        // Start server listening loop in background
        _ = Task.Run(() => _serverGame?.Start());

        SetupCommandLogging();
    }

    private void SetupCommandLogging()
    {
        var transportAdapter = _commandPublisher.Adapter;
        var transportPublisher = transportAdapter.TransportPublishers.FirstOrDefault(ta => ta is RxTransportPublisher);
        _commandLogger = _commandLoggerFactory.CreateLogger(_localizationService, _serverGame!);

        _logHandler = SafeLog(_commandLogger);
        _commandPublisher.Subscribe(_logHandler, transportPublisher);
    }

    private async Task RemoveLanPublisherAndStopHost()
    {
        if (_lanPublisher != null)
        {
            try
            {
                _commandPublisher.Adapter.RemovePublisher(_lanPublisher);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to remove LAN publisher from the transport adapter");
            }
            _lanPublisher = null;
        }

        if (_networkHostService != null && _networkHostService.IsRunning)
        {
            try
            {
                await _networkHostService.Stop();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to stop the network host service");
            }
        }
    }

    public async Task<bool> LockOnlineRoom(CancellationToken cancellationToken = default)
    {
        // Only attempt to lock when an online room is actually active and we have
        // everything required to authenticate the lock call with the relay.
        if (_onlineRelayPublisher == null
            || RoomCode == null
            || _onlineSessionToken == null
            || _relayRoomClient == null)
            return true;

        try
        {
            var lockResult = await _relayRoomClient.Lock(RoomCode, _onlineSessionToken, cancellationToken, _onlineRelayOptions);

            // Only clear the state when the lock operation succeeded, so a failed
            // attempt can be retried and the room is not considered locked prematurely.
            if (!lockResult.Success)
            {
                _logger.LogWarning("Lock relay room {RoomCode} failed: {ErrorCode} {ErrorMessage}",
                    RoomCode, lockResult.Error?.Code, lockResult.Error?.Message);
                return false;
            }

            _onlineSessionToken = null;
            RoomCode = null;
            _onlineRelayOptions = null;

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Lock relay room {RoomCode} was cancelled", RoomCode);
            return false;
        }
        catch (Exception ex)
        {
            // Do not clear state; allow caller to retry
            _logger.LogWarning(ex, "Failed to lock relay room {RoomCode}", RoomCode);
            return false;
        }
    }

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
            await _onlineRelayPublisher.RemoveAndDisposeAsync(_commandPublisher.Adapter, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to remove online publisher during StopHosting");
        }
        _onlineRelayPublisher = null;

        // Reset the session-scoped status stream once hosting is torn down
        _forwarder?.Reset();

        await RemoveLanPublisherAndStopHost();
    }

    private async Task LockRelayRoomAndCleanup(
        string? roomCode,
        string? sessionToken,
        ITransportPublisher? publisher,
        RelayClientOptions options)
    {
        // Best-effort lock of the relay room before local cleanup
        if (roomCode != null && sessionToken != null && _relayRoomClient != null)
        {
            try
            {
                await _relayRoomClient.Lock(roomCode, sessionToken, options: options);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to lock relay room {RoomCode} during failure cleanup", roomCode);
            }
        }
        
        await CleanupOnlineAfterFailure(publisher);
    }

    private async Task CleanupOnlineAfterFailure(ITransportPublisher? publisher)
    {
        // Stop forwarding online status before tearing down the relay publisher
        _forwarder?.Reset();

        // Remove and dispose the relay publisher if it was created
        await publisher.RemoveAndDisposeAsync(_commandPublisher.Adapter, _logger);
        _onlineRelayPublisher = null;
        _onlineSessionToken = null;
        _onlineRelayOptions = null;
        RoomCode = null;

        // Dispose server game if it was created
        if (_serverGame != null)
        {
            _serverGame.Dispose();
            _serverGame = null;
        }

        // Unsubscribe logging handler
        if (_logHandler != null)
        {
            _commandPublisher.Unsubscribe(_logHandler);
            _logHandler = null;
        }

        // Dispose command logger
        _commandLogger?.Dispose();
        _commandLogger = null;
    }

    public void SetBattleMap(BattleMap battleMap)
    {
        _serverGame?.SetBattleMap(battleMap);
    }

    public void TryStartGame()
    {
        _serverGame?.TryStartGame();
    }

    /// <inheritdoc />
    public bool IsGameStarted => _serverGame != null && _serverGame.TurnPhase != PhaseNames.Start;

    public string? GetLanServerAddress()
    {
        // Return address only if the host service is actually running
        return !IsLanServerRunning ? null : _networkHostService?.HubUrl;
    }
    
    public bool IsLanServerRunning => _networkHostService?.IsRunning ?? false;
    public bool CanStartLanServer => _networkHostService?.CanStart ?? false;
    public Guid? ServerGameId => _serverGame?.Id;

    public string? RoomCode { get; private set; }
    public bool IsOnlineServerRunning => _onlineRelayPublisher != null && _serverGame != null;
    public RelayClientError? OnlineError { get; private set; }

    /// <summary>
    /// Gets the connection status of the online hosting session. Remains
    /// <see cref="ConnectionStatus.NotConnected"/> while no online room is being hosted.
    /// </summary>
    public IObservable<ConnectionStatus> OnlineConnectionStatus => _forwarder?.OnlineConnectionStatus ?? _fallbackOnlineStatus;

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        // Stop forwarding online status
        _forwarder?.Reset();

        // Dispose server game if it exists
        _serverGame?.Dispose();
        _serverGame = null;

        // Remove LAN publisher from the adapter
        if (_lanPublisher != null)
        {
            try
            {
                _commandPublisher.Adapter.RemovePublisher(_lanPublisher);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to remove LAN publisher from the transport adapter during dispose");
            }
            _lanPublisher = null;
        }

        // Dispose network host
        _networkHostService?.Dispose();

        _commandLogger?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        // Stop forwarding online status
        _forwarder?.Reset();

        // Dispose server game if it exists
        _serverGame?.Dispose();
        _serverGame = null;

        // Dispose network host
        if (_networkHostService != null)
            await _networkHostService.DisposeAsync();

        // Lock the online relay room, if any, before tearing down the publisher
        await LockOnlineRoom();

        // Remove LAN publisher from the adapter
        if (_lanPublisher != null)
        {
            try
            {
                _commandPublisher.Adapter.RemovePublisher(_lanPublisher);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to remove LAN publisher from the transport adapter during async dispose");
            }
            _lanPublisher = null;
        }

        // Remove and dispose online relay publisher if it exists
        await _onlineRelayPublisher.RemoveAndDisposeAsync(_commandPublisher.Adapter, _logger);
        _onlineRelayPublisher = null;

        _commandLogger?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
