using System.ComponentModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Input;
using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Players;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.Transport.SignalR.Client.Relay;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MakaMek.Map.Services;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Presentation.ViewModels;

/// <summary>
/// Determines how the local player hosts a new game.
/// </summary>
public enum HostMode
{
    Lan,
    Online
}

public class StartNewGameViewModel : NewGameViewModel, IDisposable
{
    private readonly IGameManager _gameManager;
    private readonly ILocalizationService _localizationService;
    private readonly IClipboardService _clipboardService;
    private readonly IRelayHubConfigurationProvider _hubConfigurationProvider;
    private readonly IRelayRoomClient _relayRoomClient;
    private readonly Subject<BattleMap> _mapChanges = new();
    private IDisposable? _mapChangeSubscription;
    private CancellationTokenSource? _initCts;
    private bool _isDisposed;
    private HostMode _hostMode = HostMode.Lan;

    public StartNewGameViewModel(
        IGameManager gameManager,
        IUnitsLoader unitsLoader,
        ICommandPublisher commandPublisher,
        IDispatcherService dispatcherService,
        IGameFactory gameFactory,
        IBattleMapFactory mapFactory,
        IFileCachingService cachingService,
        IMapPreviewRenderer mapPreviewRenderer,
        IMapResourceProvider mapResourceProvider,
        IFileService fileService,
        IBotManager botManager,
        ILogger<StartNewGameViewModel> logger,
        ILocalizationService localizationService,
        IMechFactory mechFactory,
        IClipboardService clipboardService,
        IRelayHubConfigurationProvider hubConfigurationProvider,
        IRelayRoomClient relayRoomClient)
        : base(unitsLoader,
            commandPublisher,
            dispatcherService,
            gameFactory,
            cachingService,
            botManager,
            mechFactory,
            logger)
    {
        _gameManager = gameManager;
        _localizationService = localizationService;
        _clipboardService = clipboardService;
        _hubConfigurationProvider = hubConfigurationProvider;
        _relayRoomClient = relayRoomClient;
        MapConfig = new MapConfigViewModel(mapPreviewRenderer, mapFactory, mapResourceProvider, fileService, logger, dispatcherService, localizationService);
        AddPlayerCommand = new AsyncCommand(() => AddPlayer());
        AddBotCommand = new AsyncCommand(()=>AddPlayer(controlType: PlayerControlType.Bot));
        CopyRoomCodeCommand = new AsyncCommand(CopyRoomCode, _ => RoomCode != null);
        EnableMultiplayerCommand = new AsyncCommand(ExecuteEnableMultiplayer, _ => !IsMultiplayerEnabled && !HasJoinedPlayers);
    }

    /// <summary>
    /// Gets whether multiplayer networking is currently enabled.
    /// </summary>
    public bool IsMultiplayerEnabled
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            NotifyPropertyChanged();
            (EnableMultiplayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(HostingStatusText));
            NotifyPropertyChanged(nameof(CanChangeHostMode));
        }
    }

    /// <summary>
    /// Command to explicitly enable multiplayer for the currently selected host mode.
    /// </summary>
    public ICommand EnableMultiplayerCommand { get; }

    private async Task ExecuteEnableMultiplayer()
    {
        await CancelAndRestartServer();
    }

    private void OnMapConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MapConfigViewModel.Map)) return;
        var map = MapConfig.Map;
        if (map == null) return;
        _mapChanges.OnNext(map);
    }

    private void SubscribeToMapChanges()
    {
        // Broadcast the lobby map to the server once reselection settles (debounced).
        // SetBattleMap only broadcasts the map; the phase transition is triggered
        // explicitly via TryStartGame when the host starts the game.
        UnsubscribeFromMapChanges();
        MapConfig.PropertyChanged += OnMapConfigPropertyChanged;
        _mapChangeSubscription = _mapChanges
            .Throttle(TimeSpan.FromSeconds(5))
            .Subscribe(map => _gameManager.SetBattleMap(map));
    }

    private void UnsubscribeFromMapChanges()
    {
        MapConfig.PropertyChanged -= OnMapConfigPropertyChanged;
        _mapChangeSubscription?.Dispose();
        _mapChangeSubscription = null;
    }

    public async Task InitializeLobbyAndSubscribe(CancellationToken cancellationToken)
    {
        if (IsOnlineMode)
        {
            await InitializeOnlineLobbyAndSubscribe(cancellationToken);
            return;
        }

        await InitializeLanLobbyAndSubscribe(cancellationToken);
    }

    private async Task InitializeLanLobbyAndSubscribe(CancellationToken cancellationToken)
    {
        await _gameManager.InitializeLobby(cancellationToken);
        if (cancellationToken.IsCancellationRequested || _isDisposed)
        {
            IsMultiplayerEnabled = false;
            return;
        }

        SubscribeAndCreateLocalGame();

        // Update server IP initially if needed
        NotifyPropertyChanged(nameof(ServerIpAddress));
    }

    private async Task InitializeOnlineLobbyAndSubscribe(CancellationToken cancellationToken)
    {
        // Probe the active hub up front so its name/status render immediately and
        // in parallel with the room-creation round trips below, instead of leaving
        // the online section empty until InitializeLobbyOnline completes.
        ResolveActiveHubAndProbe(cancellationToken).SafeFireAndForget(
            ex => _logger.LogError(ex, "Error probing active hub"));

        await _gameManager.InitializeLobbyOnline(cancellationToken);
        if (cancellationToken.IsCancellationRequested || _isDisposed)
            return;

        if (_gameManager.OnlineError != null || _gameManager.RoomCode == null)
        {
            RoomCode = null;
            HostingError = _gameManager.OnlineError?.Message
                ?? _localizationService.GetString("Hosting_Failed");
            return;
        }

        RoomCode = _gameManager.RoomCode;
        HostingError = null;

        SubscribeAndCreateLocalGame();
    }

    private async Task ResolveActiveHubAndProbe(CancellationToken cancellationToken)
    {
        var hubs = await _hubConfigurationProvider.GetHubs();
        if (cancellationToken.IsCancellationRequested || _isDisposed || !IsOnlineMode)
            return;

        var activeHubId = await _hubConfigurationProvider.GetActiveHubId();
        if (cancellationToken.IsCancellationRequested || _isDisposed || !IsOnlineMode)
            return;

        var activeHub = hubs.FirstOrDefault(h => h.Id == activeHubId);
        var entry = activeHub == null
            ? null
            : new HubEntryViewModel(
                activeHub,
                isNew: false,
                checkStatus: CheckHubStatusAsync);
        ActiveHub = entry;
        if (entry != null)
        {
            await entry.RefreshStatusAsync(cancellationToken);
        }
    }

    private async Task<HubStatus> CheckHubStatusAsync(HubEntryViewModel entry, CancellationToken cancellationToken)
    {
        try
        {
            var options = new RelayClientOptions
            {
                BaseUrl = entry.BaseUrl,
                ApiKey = entry.ApiKey
            };
            var error = await _relayRoomClient.Health(cancellationToken, options);
            return error == null ? HubStatus.Online : HubStatus.Offline;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hub status probe failed for hub {HubName}", entry.Name);
            return HubStatus.Offline;
        }
    }

    private void SubscribeAndCreateLocalGame()
    {
        // Avoid double-subscribing if this flow runs more than once (e.g. when restarting hosting)
        _commandPublisher.Unsubscribe(HandleServerCommand);
        _commandPublisher.Subscribe(HandleServerCommand);

        // Reuse an existing local game only when it is still bound to the current
        // server game. Hosting initialization (local lobby → LAN/online) recreates
        // the server game with a new id; keeping a game bound to the old id would
        // make its ShouldHandleCommand filter drop every command from the real
        // server, silently breaking join acks and SetReady validation.
        if (_localGame != null && _localGame.ServerGameId == _gameManager.ServerGameId)
            return;

        var rxPublisher = _commandPublisher.Adapter.TransportPublishers
            .FirstOrDefault(t => t is Transport.Rx.RxTransportPublisher);
        var localPublisher = rxPublisher != null && _commandPublisher is CommandPublisher shared
            ? new LocalCommandPublisher(shared, rxPublisher)
            : null;

        CreateAndInitializeLocalGame(_gameManager.ServerGameId, localPublisher);
    }

    // Implementation of the abstract method from the base class
    protected override Task HandleCommandInternal(IGameCommand command)
    {
        switch (command)
        {
            // Handle player joining (potentially echo of local or a remote player)
            case UpdatePlayerStatusCommand statusCmd:
                var playerWithStatusUpdate = _players.FirstOrDefault(p => p.Player.Id == statusCmd.PlayerId);
                if (playerWithStatusUpdate != null && statusCmd.GameOriginId == _gameManager.ServerGameId)
                {
                    // Update player status
                    playerWithStatusUpdate.Player.Status = statusCmd.PlayerStatus;
                    playerWithStatusUpdate.RefreshStatus();
                    NotifyPropertyChanged(nameof(CanStartGame));
                    NotifyPropertyChanged(nameof(CanChangeHostMode));
                }
                break;
            
            case JoinGameCommand joinCmd:
                var existingPlayerVm = _players.FirstOrDefault(p => p.Player.Id == joinCmd.PlayerId);
                if (existingPlayerVm != null)
                {
                    // Player exists - likely the echo for a local player who just clicked Join
                    if (existingPlayerVm.IsLocalPlayer && joinCmd.GameOriginId == _gameManager.ServerGameId)
                    {
                        // Server accepted the join request
                        existingPlayerVm.Player.Status = PlayerStatus.Joined;
                        existingPlayerVm.RefreshStatus();
                        NotifyPropertyChanged(nameof(CanStartGame));
                        NotifyPropertyChanged(nameof(CanChangeHostMode));
                    }
                    // Else: Remote player sending join again? Ignore.
                }
                else
                {
                    // Player doesn't exist - must be a remote player joining
                    var remotePlayer = new Player(joinCmd.PlayerId,
                        joinCmd.PlayerName,
                        PlayerControlType.Remote,
                        joinCmd.Tint);
                    var remotePlayerVm = new PlayerViewModel(
                        remotePlayer,
                        isLocalPlayer: false, // Mark as remote
                        _ => {}, // No join action needed for remote
                        _ => {}, // No set ready action needed for remote
                        _ => Task.CompletedTask, // No show units action needed for remote
                        onUnitChanged: () => NotifyPropertyChanged(nameof(CanStartGame)));
                    
                    remotePlayerVm.AddUnits(joinCmd.Units, joinCmd.PilotAssignments); // Add units received from command
                    _players.Add(remotePlayerVm);
                    NotifyPropertyChanged(nameof(CanAddPlayer));
                    NotifyPropertyChanged(nameof(CanStartGame));
                }
                break;
        }
        return Task.CompletedTask; 
    }
    
    public MapConfigViewModel MapConfig { get; }

    public bool CanStartGame => Players.Count > 0 && Players.All(p => p.Units.Count > 0 && p.Player.Status == PlayerStatus.Ready);
    
    /// <summary>
    /// Gets the server address if LAN is running
    /// </summary>
    public string ServerIpAddress
    {
        get
        {
            var serverUrl = _gameManager.GetLanServerAddress();
            if (string.IsNullOrEmpty(serverUrl))
                return "LAN Disabled..."; // Indicate status
            try
            {
                // Extract host from the URL
                var uri = new Uri(serverUrl);
                return $"{uri.Host}"; // Display only Host name/IP
            }
            catch
            {
                return "Invalid Address"; 
            }
        }
    }
    
    public bool CanStartLanServer => _gameManager.CanStartLanServer;

    /// <summary>
    /// Gets or sets whether the game is hosted over the local network.
    /// </summary>
    public bool IsLanMode
    {
        get => _hostMode == HostMode.Lan;
        set => SetHostMode(value ? HostMode.Lan : HostMode.Online);
    }

    /// <summary>
    /// Gets or sets whether the game is hosted through the cloud relay.
    /// </summary>
    public bool IsOnlineMode
    {
        get => _hostMode == HostMode.Online;
        set => SetHostMode(value ? HostMode.Online : HostMode.Lan);
    }

    private void SetHostMode(HostMode mode)
    {
        if (_hostMode == mode) return; // Reject no-op when unchanged
        if (!CanChangeHostMode) return; // Reject while players are connected or hosting is active

        _hostMode = mode;
        NotifyPropertyChanged(nameof(IsLanMode));
        NotifyPropertyChanged(nameof(IsOnlineMode));
        // Stale hosting display state from the previous mode must not leak into the new one
        ClearHostingState();
    }

    /// <summary>
    /// Gets whether the host mode can still be changed. Once any player has joined,
    /// the lobby must not be re-created because it would disconnect the joined players.
    /// The mode is also locked while multiplayer hosting is active, since switching
    /// would tear down the live transport.
    /// </summary>
    public bool CanChangeHostMode => !HasJoinedPlayers && !IsMultiplayerEnabled;

    private bool HasJoinedPlayers => _players.Any(p => p.Player.Status is PlayerStatus.Joined or PlayerStatus.Ready);

    /// <summary>
    /// Gets the relay hub backing the online lobby, including its reachability state.
    /// Null while hosting over LAN.
    /// </summary>
    public HubEntryViewModel? ActiveHub
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Gets the room code of the online lobby, if hosting online.
    /// </summary>
    public string? RoomCode
    {
        get;
        private set
        {
            if (field == value) return; // Reject no-op when unchanged
            field = value;
            NotifyPropertyChanged();
            (CopyRoomCodeCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(HostingStatusText));
        }
    }

    /// <summary>
    /// Gets the last hosting error, if any.
    /// </summary>
    public string? HostingError
    {
        get;
        private set
        {
            if (field == value) return; // Reject no-op when unchanged
            field = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(HostingStatusText));
        }
    }

    /// <summary>
    /// Gets the localized status text for the current hosting state.
    /// </summary>
    public string HostingStatusText
    {
        get
        {
            if (IsHosting)
                return _localizationService.GetString("Hosting_Starting");
            if (HostingError != null)
                return HostingError;
            if (IsMultiplayerEnabled && RoomCode != null)
                return string.Format(_localizationService.GetString("Hosting_RoomReady"), RoomCode);
            if (IsMultiplayerEnabled && IsLanMode && _gameManager.IsLanServerRunning)
                return _localizationService.GetString("Hosting_LanEnabled");
            if (IsMultiplayerEnabled)
                return _localizationService.GetString("Hosting_Starting");
            return string.Empty;
        }
    }

    private bool IsHosting
    {
        get;
        set
        {
            if (field == value) return; // Reject no-op when unchanged
            field = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(HostingStatusText));
        }
    }

    /// <summary>
    /// Gets or sets whether the last room-code copy attempt succeeded. Null before the first attempt.
    /// </summary>
    public bool? RoomCodeCopySucceeded
    {
        get;
        private set
        {
            if (field == value) return; // Reject no-op when unchanged
            field = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(CopyRoomCodeStatusText));
        }
    }

    /// <summary>
    /// Gets the localized status text for the last room-code copy attempt. Empty before the first attempt.
    /// </summary>
    public string CopyRoomCodeStatusText
    {
        get
        {
            return RoomCodeCopySucceeded switch
            {
                true => _localizationService.GetString("Network_CopyRoomCode_Success"),
                false => _localizationService.GetString("Network_CopyRoomCode_Failed"),
                _ => string.Empty
            };
        }
    }

    private void ClearHostingState()
    {
        IsHosting = false;
        ActiveHub = null;
        RoomCode = null;
        HostingError = null;
        RoomCodeCopySucceeded = null;
    }

    /// <summary>
    /// Copies the room code to the clipboard (enabled while an online room exists).
    /// </summary>
    public ICommand CopyRoomCodeCommand { get; }

    /// <summary>
    /// Restarts hosting for the currently selected mode. No-op while any player has joined.
    /// </summary>
    public async Task CancelAndRestartServer()
    {
        if (HasJoinedPlayers) return;

        if (_initCts is not null)
        {
            await _initCts.CancelAsync();
            if (HasJoinedPlayers) return; // A player joined while cancellation was pending; keep the live lobby
        }
        _initCts?.Dispose();
        _initCts = new CancellationTokenSource();

        try
        {
            IsHosting = true;
            try
            {
                await InitializeLobbyAndSubscribe(_initCts.Token);
                // Multiplayer is only considered enabled once hosting completed
                // without errors; a failed or cancelled initialization keeps the
                // enable command available for retry.
                IsMultiplayerEnabled = HostingError == null;
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Lobby initialization cancelled by superseded restart or detach/dispose");
                IsMultiplayerEnabled = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing lobby during restart");
                IsMultiplayerEnabled = false;
            }
        }
        finally
        {
            IsHosting = false;
        }
    }

    private async Task CopyRoomCode()
    {
        if (RoomCode == null) return;
        RoomCodeCopySucceeded = await _clipboardService.SetText(RoomCode);
    }

    public bool IsNetworkSectionExpanded
    {
        get;
        set => SetProperty(ref field, value);
    }

    public void ToggleNetworkSection()
    {
        IsNetworkSectionExpanded = !IsNetworkSectionExpanded;
    }

    public ICommand StartGameCommand => new AsyncCommand(NavigateToBattleMap);

    private async Task NavigateToBattleMap()
    {
        if (MapConfig.Map == null) return;
        // Use the map generated by MapConfigViewModel to ensure preview and game map are identical
        var map = MapConfig.Map;

        // Lock the online relay room now that the lobby is transitioning to deployment;
        // LAN hosting has no relay room to lock.
        if (IsOnlineMode)
        {
            var lockSucceeded = await _gameManager.LockOnlineRoom();
            if (!lockSucceeded)
            {
                HostingError = _gameManager.OnlineError?.Message
                    ?? _localizationService.GetString("Hosting_Failed");
                return;
            }
        }

        // Set BattleMap on GameManager/ServerGame (propagates to clients via the command system)
        _gameManager.SetBattleMap(map);

        // Cancel any pending debounced map re-broadcast so no stale SetBattleMapCommand
        // can be published after the Start → Deployment transition below.
        UnsubscribeFromMapChanges();

        // Explicitly trigger the Start → Deployment transition now that the map is set
        // and all players are ready. SetBattleMap no longer transitions on its own.
        _gameManager.TryStartGame();

        // Host Client for local player(s)
        var battleMapViewModel = await NavigationService.GetNewViewModelAsync<BattleMapViewModel>();
        if (battleMapViewModel == null)
        {
            throw new Exception("BattleMapViewModel is not registered");
        }
        battleMapViewModel.Game = _localGame;

        // Navigate to BattleMap view
        await NavigationService.NavigateToViewModelAsync(battleMapViewModel);
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
            () => NotifyPropertyChanged(nameof(CanStartGame)),
            isDefaultPlayer
                ? OnDefaultPlayerNameChanged
                : null,
            isDefaultPlayer);
    }
    
    // Override the base AddPlayer to add additional notification
    protected override Task AddPlayer(
        PlayerData? playerData = null,
        PlayerControlType controlType = PlayerControlType.Human)
    {
        var result = base.AddPlayer(playerData, controlType);
        NotifyPropertyChanged(nameof(CanStartGame)); // CanStartGame might be false until units are added
        return result;
    }
    
    // Override the base RemovePlayer to add additional notification
    protected override Task RemovePlayer(PlayerViewModel? playerVm)
    {
        var result = base.RemovePlayer(playerVm);
        NotifyPropertyChanged(nameof(CanStartGame));
        return result;
    }

    // Implementation of abstract property from base class
    public override bool CanAddPlayer => _players.Count < 4; // Limit to 4 players for now
    
    // Implementation of abstract property from base class
    public override bool CanPublishCommands => true; // TODO: is it actually always true?

    public void Dispose()
    {
        _isDisposed = true;
        _initCts?.Cancel();
        _initCts?.Dispose();
        _initCts = null;
        _commandPublisher.Unsubscribe(HandleServerCommand);
        UnsubscribeFromMapChanges();
        MapConfig.Dispose();
        _mapChanges.Dispose();
        GC.SuppressFinalize(this);
    }

    public override void DetachHandlers()
    {
        _initCts?.Cancel();
        _initCts?.Dispose();
        _initCts = null;
        // Once the game has started, the hosting session belongs to the running game;
        // stopping it here would disconnect the host's relay/LAN transport mid-game.
        if (IsMultiplayerEnabled && !_gameManager.IsGameStarted)
        {
            _gameManager.StopHosting().SafeFireAndForget(
                ex => _logger.LogError(ex, "Error stopping hosting on detach"));
        }
        UnsubscribeFromMapChanges();
        base.DetachHandlers();
        _commandPublisher.Unsubscribe(HandleServerCommand);
    }

    public override void AttachHandlers()
    {
        SubscribeToMapChanges();

        if (HasJoinedPlayers)
        {
            base.AttachHandlers();
            return; // Don't reset hosting state or restart hosting while players are connected
        }

        ResetHostingState();
        base.AttachHandlers();
        RestartLocalLobbyInitialization();
    }

    private void RestartLocalLobbyInitialization()
    {
        _initCts?.Cancel();
        _initCts?.Dispose();
        _initCts = new CancellationTokenSource();
        var cancellationToken = _initCts.Token;
        InitializeLocalLobbyAndSubscribe(cancellationToken).SafeFireAndForget(
            ex => _logger.LogError(ex, "Error initializing local lobby"));
    }

    private async Task InitializeLocalLobbyAndSubscribe(CancellationToken cancellationToken)
    {
        // The local game must only be created once the manager has finished
        // resetting and created the new server game, so it binds to the correct
        // server game id.
        var initialized = await _gameManager.InitializeLocalLobby(cancellationToken);
        if (!initialized || cancellationToken.IsCancellationRequested || _isDisposed)
            return;

        SubscribeAndCreateLocalGame();
    }

    private void ResetHostingState()
    {
        var defaultMode = CanStartLanServer ? HostMode.Lan : HostMode.Online;
        if (_hostMode != defaultMode)
        {
            _hostMode = defaultMode;
            NotifyPropertyChanged(nameof(IsLanMode));
            NotifyPropertyChanged(nameof(IsOnlineMode));
        }
        IsMultiplayerEnabled = false;
        ClearHostingState();
    }
}
