using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Services.Logging;
using Sanet.MakaMek.Core.Services.Logging.Factories;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Services.Transport.Relay;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;
using Sanet.Transport.Rx;
using Sanet.Transport.SignalR.Client.Publishers;

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
    private readonly IRelayPublisherFactory? _relayPublisherFactory;
    private readonly IRelayHubConfigurationProvider? _relayHubConfigurationProvider;
    private RelayClientPublisher? _onlineRelayPublisher;
    private string? _onlineSessionToken;

    public GameManager(ICommandPublisher commandPublisher,
        IGameFactory gameFactory,
        ILocalizationService localizationService,
        ICommandLoggerFactory commandLoggerFactory,
        ILogger<GameManager> logger,
        INetworkHostService? networkHostService = null,
        IRelayRoomClient? relayRoomClient = null,
        IRelayPublisherFactory? relayPublisherFactory = null,
        IRelayHubConfigurationProvider? relayHubConfigurationProvider = null)
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
        // Remove and dispose any stale relay publisher before re-hosting
        await RemoveAndDisposeOnlinePublisherAsync(_onlineRelayPublisher);
        _onlineRelayPublisher = null;

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

    public async Task InitializeLobby()
    {
        // Reset before initializing new lobby
        await ResetForNewGame();

        var transportAdapter = _commandPublisher.Adapter;
        // Start the network host if supported and not already running
        if (CanStartLanServer && !IsLanServerRunning && _networkHostService != null)
        {
            await _networkHostService.Start();

            // Add the network publisher to the transport adapter if successfully started
            if (_networkHostService.Publisher != null)
            {
                transportAdapter.AddPublisher(_networkHostService.Publisher);
            }
        }

        CreateServerGameAndSetupLogging();
    }

    public async Task InitializeLobbyOnline(CancellationToken cancellationToken = default)
    {
        OnlineError = null;

        // Close the currently active relay room before resetting state
        var closeSucceeded = await CloseOnlineRoom(cancellationToken);
        if (!closeSucceeded)
        {
            OnlineError = new RelayClientError(
                RelayClientErrorCode.Unknown,
                "Failed to close the currently active relay room.");
            return;
        }

        RoomCode = null;

        // Reset before initializing new lobby (also clears any stale relay publisher)
        await ResetForNewGame();

        // Relay hosting requires the room client, the publisher factory, and an active hub configuration
        if (_relayRoomClient is null
            || _relayPublisherFactory is null
            || _relayHubConfigurationProvider is null
            || string.IsNullOrWhiteSpace(_relayHubConfigurationProvider.ActiveBaseUrl))
        {
            OnlineError = new RelayClientError(
                RelayClientErrorCode.ConfigurationError,
                "Relay hosting is not configured on this platform.");
            return;
        }

        // The server game must exist before the relay room is created so the host
        // game id can be reported to the Hub and shared with joiners.
        CreateServerGameAndSetupLogging();
        var gameId = _serverGame!.Id;

        var createResult = await _relayRoomClient.CreateAsync(
            gameId,
            cancellationToken);
        if (!createResult.Success
            || createResult.RoomCode is null
            || createResult.SessionToken is null
            || createResult.HostGameId is null)
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
            await CleanupOnlineAfterFailureAsync(publisher: null);
            return;
        }

        RelayClientPublisher? publisher = null;
        try
        {
            var baseUrl = _relayHubConfigurationProvider.ActiveBaseUrl;
            var hubUrl = RelayHubDefaults.BuildHubUrl(baseUrl);

            publisher = await _relayPublisherFactory.CreateAsync(
                hubUrl,
                createResult.RoomCode,
                createResult.SessionToken,
                createResult.HostGameId.Value,
                _relayHubConfigurationProvider.ActiveApiKey,
                cancellationToken);

            _commandPublisher.Adapter.AddPublisher(publisher);
            _onlineRelayPublisher = publisher;

            var readyResult = await _relayRoomClient.ReadyAsync(
                createResult.RoomCode,
                createResult.SessionToken,
                cancellationToken);
            if (!readyResult.Success)
            {
                OnlineError = readyResult.Error
                    ?? new RelayClientError(
                        RelayClientErrorCode.Unknown,
                        "The relay did not confirm the room as ready.");
                await CloseRelayRoomAndCleanupAsync(createResult.RoomCode, createResult.SessionToken, publisher);
                return;
            }

            RoomCode = createResult.RoomCode;
            _onlineSessionToken = createResult.SessionToken;
            _logger.LogInformation(
                "Hosted relay room {RoomCode} for game {GameId}; relay publisher connected",
                createResult.RoomCode,
                gameId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await CloseRelayRoomAndCleanupAsync(createResult.RoomCode, createResult.SessionToken, publisher);
            throw;
        }
        catch (Exception)
        {
            OnlineError = new RelayClientError(
                RelayClientErrorCode.Unknown,
                "Failed to connect the host to the relay.");
            await CloseRelayRoomAndCleanupAsync(createResult.RoomCode, createResult.SessionToken, publisher);
        }
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

    private async Task RemoveAndDisposeOnlinePublisherAsync(RelayClientPublisher? publisher)
    {
        // Remove and dispose the relay publisher if it was created
        if (publisher == null) return;
        try
        {
            _commandPublisher.Adapter.RemovePublisher(publisher);
        }
        catch
        {
            // Swallow to avoid masking the original failure
        }
        try
        {
            await publisher.DisposeAsync();
        }
        catch
        {
            // Swallow to avoid masking the original failure
        }
    }

    public async Task<bool> CloseOnlineRoom(CancellationToken cancellationToken = default)
    {
        // Only attempt to close when an online room is actually active and we have
        // everything required to authenticate the close call with the relay.
        if (_onlineRelayPublisher == null
            || RoomCode == null
            || _onlineSessionToken == null
            || _relayRoomClient == null)
            return true;

        try
        {
            var closeResult = await _relayRoomClient.CloseAsync(RoomCode, _onlineSessionToken, cancellationToken);

            // Only clear the state when the close operation succeeded, so a failed
            // attempt can be retried and the room is not considered closed prematurely.
            if (!closeResult.Success)
            {
                _logger.LogWarning("Close relay room {RoomCode} failed: {ErrorCode} {ErrorMessage}",
                    RoomCode, closeResult.Error?.Code, closeResult.Error?.Message);
                return false;
            }

            _onlineSessionToken = null;
            RoomCode = null;

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Close relay room {RoomCode} was cancelled", RoomCode);
            return false;
        }
        catch (Exception ex)
        {
            // Do not clear state; allow caller to retry
            _logger.LogWarning(ex, "Failed to close relay room {RoomCode}", RoomCode);
            return false;
        }
    }

    private async Task CloseRelayRoomAndCleanupAsync(string? roomCode, string? sessionToken, RelayClientPublisher? publisher)
    {
        // Best-effort close of the relay room before local cleanup
        if (roomCode != null && sessionToken != null && _relayRoomClient != null)
        {
            try
            {
                await _relayRoomClient.CloseAsync(roomCode, sessionToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to close relay room {RoomCode} during failure cleanup", roomCode);
            }
        }
        
        await CleanupOnlineAfterFailureAsync(publisher);
    }

    private async Task CleanupOnlineAfterFailureAsync(RelayClientPublisher? publisher)
    {
        // Remove and dispose the relay publisher if it was created
        await RemoveAndDisposeOnlinePublisherAsync(publisher);
        _onlineRelayPublisher = null;
        _onlineSessionToken = null;
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

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        // Dispose server game if it exists
        _serverGame?.Dispose();
        _serverGame = null;

        // Dispose network host
        _networkHostService?.Dispose();

        _commandLogger?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        // Dispose server game if it exists
        _serverGame?.Dispose();
        _serverGame = null;

        // Dispose network host
        _networkHostService?.Dispose();

        // Close the online relay room, if any, before tearing down the publisher
        await CloseOnlineRoom();

        // Remove and dispose online relay publisher if it exists
        await RemoveAndDisposeOnlinePublisherAsync(_onlineRelayPublisher);
        _onlineRelayPublisher = null;

        _commandLogger?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
