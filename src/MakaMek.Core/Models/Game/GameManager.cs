using Microsoft.Extensions.Options;
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
    private ICommandLogger? _commandLogger;
    private Action<IGameCommand>? _logHandler;
    private readonly IRelayRoomClient? _relayRoomClient;
    private readonly IRelayPublisherFactory? _relayPublisherFactory;
    private readonly IOptions<RelayClientOptions>? _relayOptions;
    private RelayClientPublisher? _onlineRelayPublisher;

    public GameManager(ICommandPublisher commandPublisher,
        IGameFactory gameFactory,
        ILocalizationService localizationService,
        ICommandLoggerFactory commandLoggerFactory,
        INetworkHostService? networkHostService = null,
        IRelayRoomClient? relayRoomClient = null,
        IRelayPublisherFactory? relayPublisherFactory = null,
        IOptions<RelayClientOptions>? relayOptions = null)
    {
        _commandPublisher = commandPublisher;
        _gameFactory = gameFactory;
        _localizationService = localizationService;
        _commandLoggerFactory = commandLoggerFactory;
        _networkHostService = networkHostService;
        _relayRoomClient = relayRoomClient;
        _relayPublisherFactory = relayPublisherFactory;
        _relayOptions = relayOptions;
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

    public async Task InitializeLobbyOnline(
        Guid playerId,
        string playerName,
        CancellationToken cancellationToken = default)
    {
        OnlineError = null;
        RoomCode = null;
        _onlineRelayPublisher = null;

        // Reset before initializing new lobby
        await ResetForNewGame();

        // Relay hosting requires both the room client and the publisher factory
        if (_relayRoomClient is null || _relayPublisherFactory is null)
        {
            OnlineError = new RelayClientError(
                RelayClientErrorCode.ConfigurationError,
                "Relay hosting is not configured on this platform.");
            return;
        }

        var createResult = await _relayRoomClient.CreateAsync(
            playerId,
            playerName,
            cancellationToken);
        if (!createResult.Success
            || createResult.RoomCode is null
            || createResult.SessionToken is null
            || createResult.HostId is null)
        {
            OnlineError = createResult.Error
                ?? new RelayClientError(
                    RelayClientErrorCode.Unknown,
                    "The relay did not return the values required to host.");
            return;
        }

        // Create the game server instance
        _serverGame = _gameFactory.CreateServerGame(_commandPublisher);
        // Start server listening loop in background
        _ = Task.Run(() => _serverGame?.Start());

        SetupCommandLogging();

        RelayClientPublisher? publisher = null;
        try
        {
            var baseUrl = _relayOptions?.Value.BaseUrl ?? string.Empty;
            var hubUrl = $"{baseUrl.TrimEnd('/')}{RelayHubDefaults.HubPath}";

            publisher = await _relayPublisherFactory.CreateAsync(
                hubUrl,
                createResult.RoomCode,
                createResult.SessionToken,
                createResult.HostId.Value);

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
                await CleanupOnlineAfterFailureAsync(publisher);
                return;
            }

            RoomCode = createResult.RoomCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            OnlineError = new RelayClientError(
                RelayClientErrorCode.Unknown,
                "Failed to connect the host to the relay.");
            await CleanupOnlineAfterFailureAsync(publisher);
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

    private async Task CleanupOnlineAfterFailureAsync(RelayClientPublisher? publisher)
    {
        // Remove and dispose the relay publisher if it was created
        if (publisher != null)
        {
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
        _onlineRelayPublisher = null;
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

        // Dispose online relay publisher if it exists
        if (_onlineRelayPublisher != null)
        {
            try
            {
                _onlineRelayPublisher.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch
            {
                // Swallow to avoid breaking disposal
            }
            _onlineRelayPublisher = null;
        }

        _commandLogger?.Dispose();
    }
}
