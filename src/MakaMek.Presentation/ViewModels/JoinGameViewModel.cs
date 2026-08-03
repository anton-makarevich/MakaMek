using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Bots.Services;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Services.Transport.Relay;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Presentation.Models.Logger;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MakaMek.Services;
using Sanet.Transport;
using Sanet.Transport.SignalR.Client.Publishers;

namespace Sanet.MakaMek.Presentation.ViewModels;

/// <summary>
/// Determines how the local player joins a game.
/// </summary>
public enum JoinMode
{
    Lan,
    Online
}

public class JoinGameViewModel : NewGameViewModel, IDisposable
{
    private readonly ITransportFactory _transportFactory;
    private readonly IRelayRoomClient? _relayRoomClient;
    private readonly IRelayPublisherFactory? _relayPublisherFactory;
    private readonly IOptions<RelayClientOptions>? _relayOptions;
    private readonly ILocalizationService _localizationService;
    private JoinMode _joinMode = JoinMode.Lan;
    private RelayClientPublisher? _onlineRelayPublisher;
    private CancellationTokenSource? _activeJoinCts;

    public JoinGameViewModel(
        IUnitsLoader unitsLoader,
        ICommandPublisher commandPublisher,
        IDispatcherService dispatcherService,
        IGameFactory gameFactory,
        ITransportFactory transportFactory,
        IFileCachingService cachingService,
        IBotManager botManager,
        ILogger<JoinGameViewModel> logger,
        IMechFactory mechFactory,
        IRelayRoomClient? relayRoomClient = null,
        IRelayPublisherFactory? relayPublisherFactory = null,
        IOptions<RelayClientOptions>? relayOptions = null,
        ILocalizationService? localizationService = null)
        : base(unitsLoader,
            commandPublisher,
            dispatcherService,
            gameFactory,
            cachingService,
            botManager,
            mechFactory,
            logger)
    {
        _transportFactory = transportFactory;
        _relayRoomClient = relayRoomClient;
        _relayPublisherFactory = relayPublisherFactory;
        _relayOptions = relayOptions;
        _localizationService = localizationService ?? new FakeLocalizationService();

        AddPlayerCommand = new AsyncCommand(() => AddPlayer());
        AddBotCommand = new AsyncCommand(()=>AddPlayer(controlType: PlayerControlType.Bot));
        ConnectCommand = new AsyncCommand(ConnectToServer, (_)=>CanConnect);
        JoinRoomCommand = new AsyncCommand(JoinRoom, (_) => CanJoin);
    }

    // Implementation of the abstract method from a base class
    protected override async Task HandleCommandInternal(IGameCommand command)
    {
        switch (command)
        {
            case UpdatePlayerStatusCommand statusCmd:
                var playerWithStatusUpdate = _players.FirstOrDefault(p => p.Player.Id == statusCmd.PlayerId);
                if (playerWithStatusUpdate != null) // Simplified check for join view
                {
                    playerWithStatusUpdate.Player.Status = statusCmd.PlayerStatus;
                    playerWithStatusUpdate.RefreshStatus();
                    // Potentially update CanStartGame equivalent if needed
                }
                break;

            case JoinGameCommand joinCmd:
                var existingPlayerVm = _players.FirstOrDefault(p => p.Player.Id == joinCmd.PlayerId);
                if (existingPlayerVm == null) // Add if it's a new remote player
                {
                     var newRemotePlayer = new Player(joinCmd.PlayerId,
                         joinCmd.PlayerName,
                         PlayerControlType.Remote,
                         joinCmd.Tint); 
                     var remotePlayerViewModel = new PlayerViewModel(
                        newRemotePlayer,
                        isLocalPlayer: false,
                        _ => {}, // Remote players don't publish join
                        _ => {}); // No callback for ready state
                     remotePlayerViewModel.AddUnits(joinCmd.Units, joinCmd.PilotAssignments); // Add units received from command
                     _players.Add(remotePlayerViewModel);
                }
                else if (existingPlayerVm.IsLocalPlayer)
                {
                     // Handle join confirmation for local player
                     existingPlayerVm.Player.Status = PlayerStatus.Joined;
                     existingPlayerVm.RefreshStatus();
                }
                break;
                
            case SetBattleMapCommand:
                // Handle navigation to BattleMapViewModel when the battle map is set
                
                // Get the BattleMapViewModel and set the game
                var battleMapViewModel = NavigationService.GetViewModel<BattleMapViewModel>();
                if (battleMapViewModel == null)
                {
                    throw new Exception("BattleMapViewModel is not registered");
                }
                battleMapViewModel.Game = _localGame;
                
                // Navigate to BattleMap view
                await NavigationService.NavigateToViewModelAsync(battleMapViewModel);
                
                break;
        }
        
        // Refresh bindings
        NotifyPropertyChanged(nameof(Players));
    }

    public string ServerIp
    {
        get;
        set
        {
            SetProperty(ref field, value);
            (ConnectCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(CanConnect));
            NotifyPropertyChanged(nameof(ServerAddress));
            NotifyPropertyChanged(nameof(CanAddPlayer));
        }
    } = string.Empty;

    public string ServerAddress => $"http://{ServerIp}:2439/makamekhub";

    public bool IsConnected
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            // Update UI based on connection status
            foreach (var player in _players)
            {
                player.RefreshStatus();
            }
            (JoinRoomCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(CanJoin));
        }
    }

    public ICommand ConnectCommand { get; private set; }

    public bool CanConnect => !string.IsNullOrWhiteSpace(ServerIp) && !IsConnected;

    /// <summary>
    /// Gets or sets whether the join uses the local network.
    /// </summary>
    public bool IsLanMode
    {
        get => _joinMode == JoinMode.Lan;
        set => SetJoinMode(value ? JoinMode.Lan : JoinMode.Online);
    }

    /// <summary>
    /// Gets or sets whether the join uses the cloud relay.
    /// </summary>
    public bool IsOnlineMode
    {
        get => _joinMode == JoinMode.Online;
        set => SetJoinMode(value ? JoinMode.Online : JoinMode.Lan);
    }

    private void SetJoinMode(JoinMode mode)
    {
        if (_joinMode == mode) return; // Reject no-op when unchanged

        _joinMode = mode;
        NotifyPropertyChanged(nameof(IsLanMode));
        NotifyPropertyChanged(nameof(IsOnlineMode));
        _activeJoinCts?.Cancel();
        ClearJoinState();
    }

    /// <summary>
    /// Gets or sets the room code used to join an online game.
    /// </summary>
    public string RoomCode
    {
        get;
        set
        {
            var normalized = value.Trim().ToUpperInvariant();
            if (field == normalized) return; // Reject no-op when unchanged
            field = normalized;
            NotifyPropertyChanged();
            (JoinRoomCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(CanJoin));
        }
    } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether an online join can be attempted.
    /// </summary>
    public bool CanJoin => !IsJoining && !string.IsNullOrWhiteSpace(RoomCode) && !IsConnected;

    /// <summary>
    /// Joins an online game by room code through the cloud relay.
    /// </summary>
    public ICommand JoinRoomCommand { get; private set; }

    private bool IsJoining
    {
        get;
        set
        {
            if (field == value) return; // Reject no-op when unchanged
            field = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(JoinStatusText));
            (JoinRoomCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(CanJoin));
        }
    }

    /// <summary>
    /// Gets the last online join error, if any.
    /// </summary>
    public string? JoinError
    {
        get;
        private set
        {
            if (field == value) return; // Reject no-op when unchanged
            field = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(JoinStatusText));
        }
    }

    /// <summary>
    /// Gets the localized status text for the current online join state.
    /// </summary>
    public string JoinStatusText
    {
        get
        {
            if (IsJoining)
                return _localizationService.GetString("Join_Connecting");
            if (JoinError != null)
                return JoinError;
            return string.Empty;
        }
    }

    private void ClearJoinState()
    {
        IsJoining = false;
        JoinError = null;
    }

    private async Task JoinRoom()
    {
        if (!CanJoin) return;

        IsJoining = true;
        JoinError = null;

        try
        {
            // Online joining requires the relay room client, publisher factory, and options
            if (_relayRoomClient is null || _relayPublisherFactory is null || _relayOptions is null)
            {
                JoinError = _localizationService.GetString("Join_ConfigurationError");
                return;
            }

            _activeJoinCts = new CancellationTokenSource();

            var playerData = GetLocalPlayerData();
            var result = await _relayRoomClient.JoinAsync(RoomCode, playerData.Id, playerData.Name, _activeJoinCts.Token);
            if (_joinMode != JoinMode.Online) return;
            if (!result.Success || result.SessionToken is null || result.HostId is null)
            {
                JoinError = GetJoinErrorText(result.Error);
                return;
            }

            var baseUrl = _relayOptions?.Value.BaseUrl ?? string.Empty;
            var hubUrl = RelayHubDefaults.BuildHubUrl(baseUrl);
            var publisher = await _relayPublisherFactory.CreateAsync(
                hubUrl,
                RoomCode,
                result.SessionToken,
                result.HostId.Value);
            _onlineRelayPublisher = publisher;

            var adapter = _commandPublisher.Adapter;

            // Clear any existing publishers and prepare for a new connection
            await adapter.ClearPublishers();
            adapter.AddPublisher(publisher);
            adapter.RegisterDisconnectHandler(OnRelayHostDisconnected);
            _commandPublisher.Subscribe(HandleServerCommand);

            if (_localGame != null)
            {
                _localGame.Dispose();
                _localGame = null;
            }
            _localGame = _gameFactory.CreateClientGame(_commandPublisher);

            // Initialize BotManager with the ClientGame and DecisionEngineProvider
            var decisionEngineProvider = new DecisionEngineProvider(_localGame);
            _botManager.Initialize(_localGame, decisionEngineProvider);

            IsConnected = true;
            _localGame.RequestLobbyStatus(new RequestGameLobbyStatusCommand
            {
                GameOriginId = _localGame.Id
            });
            (JoinRoomCommand as AsyncCommand)?.RaiseCanExecuteChanged(); // Disable join button
            NotifyPropertyChanged(nameof(CanAddPlayer)); // Enable Add Player once connected
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Online game join was cancelled");
        }
        catch (Exception ex)
        {
            _commandPublisher.Unsubscribe(HandleServerCommand);
            _logger.LogError(ex, "Error joining online game");
            await RemoveAndDisposeOnlinePublisherAsync();
            IsConnected = false;
            JoinError = _localizationService.GetString("Join_ConnectionFailed");
        }
        finally
        {
            _activeJoinCts?.Dispose();
            _activeJoinCts = null;
            IsJoining = false;
        }
    }

    /// <summary>
    /// Invoked when the relay publisher reports that the host has disconnected from the room.
    /// Synthesizes a local <see cref="GameEndedCommand"/> so the client reacts the same way
    /// it would if the server had sent the command, without requiring further network traffic.
    /// </summary>
    /// <param name="publisher">The publisher that lost its connection to the host.</param>
    private void OnRelayHostDisconnected(ITransportPublisher publisher)
    {
        if (_localGame == null || _localGame.IsDisposed) return;

        var command = new GameEndedCommand
        {
            GameOriginId = Guid.NewGuid(),
            Reason = GameEndReason.HostDisconnected,
            Timestamp = DateTime.UtcNow
        };
        _localGame.HandleCommand(command);
    }

    private async Task RemoveAndDisposeOnlinePublisherAsync()
    {
        if (_onlineRelayPublisher == null) return;
        try
        {
            _commandPublisher.Adapter.RemovePublisher(_onlineRelayPublisher);
        }
        catch (Exception ex)
        {
            // Swallow to avoid masking the original failure, but log the cleanup issue
            _logger.LogWarning(ex, "Failed to remove online relay publisher during cleanup");
        }
        try
        {
            await DisposeOnlinePublisherAsync(_onlineRelayPublisher);
        }
        catch (Exception ex)
        {
            // Swallow to avoid masking the original failure, but log the cleanup issue
            _logger.LogWarning(ex, "Failed to dispose online relay publisher during cleanup");
        }
        _onlineRelayPublisher = null;
    }

    protected virtual ValueTask DisposeOnlinePublisherAsync(RelayClientPublisher publisher) =>
        publisher.DisposeAsync();

    private string GetJoinErrorText(RelayClientError? error)
    {
        var key = error?.Code switch
        {
            RelayClientErrorCode.RoomNotFound => "Join_InvalidCode",
            RelayClientErrorCode.RoomExpired => "Join_RoomExpired",
            RelayClientErrorCode.HostNotReady => "Join_HostNotReady",
            RelayClientErrorCode.RoomFull => "Join_RoomFull",
            RelayClientErrorCode.HubAtCapacity => "Join_HubAtCapacity",
            RelayClientErrorCode.RateLimited => "Join_RateLimited",
            RelayClientErrorCode.NetworkError or RelayClientErrorCode.Timeout => "Join_ConnectionFailed",
            RelayClientErrorCode.ConfigurationError => "Join_ConfigurationError",
            _ => "Join_Failed"
        };
        return _localizationService.GetString(key);
    }

    private async Task ConnectToServer()
    {
        if (!CanConnect) return;

        try
        {
            // Get access to the adapter from the command publisher
            var adapter = _commandPublisher.Adapter;
            
            // Clear any existing publishers and prepare for a new connection
            await adapter.ClearPublishers();
            // Any previously active relay publisher was disposed by the adapter above
            _onlineRelayPublisher = null;
            
            // Create a network client publisher using the factory and connect
            var client = await _transportFactory.CreateAndStartClientPublisher(ServerAddress);
            adapter.AddPublisher(client);
            _commandPublisher.Subscribe(HandleServerCommand);
            if (_localGame != null)
            {
                _localGame.Dispose();
                _localGame = null;
            }
            _localGame = _gameFactory.CreateClientGame(_commandPublisher);

            _localGame.Logger.LogAttemptedToConnectToServerIp(ServerIp);
            
            // Initialize BotManager with the ClientGame and DecisionEngineProvider
            var decisionEngineProvider = new DecisionEngineProvider(_localGame);
            _botManager.Initialize(_localGame, decisionEngineProvider);

            IsConnected = true;
            _localGame.RequestLobbyStatus(new RequestGameLobbyStatusCommand
            {
                GameOriginId = _localGame.Id
            });
            (ConnectCommand as AsyncCommand)?.RaiseCanExecuteChanged(); // Disable connect button
            NotifyPropertyChanged(nameof(CanAddPlayer)); // Enable Add Player once connected
        }
        catch (Exception ex)
        {
            _localGame?.Logger.LogError(ex, "Error connecting to server: {Message}", ex.Message);
            IsConnected = false;
        }
    }

    public async Task Disconnect()
    {
        if (_activeJoinCts != null)
            await _activeJoinCts.CancelAsync();
        if (_localGame != null)
        {
            _localGame.Dispose();
            _localGame = null;
        }
        _commandPublisher.Unsubscribe(HandleServerCommand);
        await RemoveAndDisposeOnlinePublisherAsync();
        await _commandPublisher.Adapter.ClearPublishers();
        IsConnected = false;
        (ConnectCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        NotifyPropertyChanged(nameof(CanAddPlayer));
    }

    public void Dispose()
    {
        Disconnect().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    // Implementation of template method from base class
    protected override PlayerViewModel CreatePlayerViewModel(Player player, bool isDefaultPlayer = false)
    {
        return new PlayerViewModel(
            player,
            isLocalPlayer: true,
            PublishJoinCommand,
            PublishSetReadyCommand,
            ShowAvailableUnitsTable,
            ShowUnitInfo,
            null,
            isDefaultPlayer
                ? OnDefaultPlayerNameChanged
                : null,
            isDefaultPlayer,
            () => IsConnected);
    }

    // Implementation of abstract property from base class
    // Allow adding default player before connection, but require connection for additional players
    public override bool CanAddPlayer => (IsConnected || _players.Count == 0) && _players.Count < 4;

    // Implementation of abstract property from base class
    public override bool CanPublishCommands => IsConnected;
}
