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
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Factories;
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
        IMechFactory mechFactory)
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
        MapConfig = new MapConfigViewModel(mapPreviewRenderer, mapFactory, mapResourceProvider, fileService, logger, dispatcherService, localizationService);
        AddPlayerCommand = new AsyncCommand(() => AddPlayer());
        AddBotCommand = new AsyncCommand(()=>AddPlayer(controlType: PlayerControlType.Bot));
        CopyRoomCodeCommand = new AsyncCommand(CopyRoomCode, _ => RoomCode != null);
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
        await _gameManager.InitializeLobby();
        if (cancellationToken.IsCancellationRequested || _isDisposed)
            return;

        SubscribeAndCreateLocalGame();

        // Update server IP initially if needed
        NotifyPropertyChanged(nameof(ServerIpAddress));
    }

    private async Task InitializeOnlineLobbyAndSubscribe(CancellationToken cancellationToken)
    {
        var playerData = GetLocalPlayerData();
        await _gameManager.InitializeLobbyOnline(playerData.Id, playerData.Name, cancellationToken);
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

    private void SubscribeAndCreateLocalGame()
    {
        // Avoid double-subscribing if this flow runs more than once (e.g. when restarting hosting)
        _commandPublisher.Unsubscribe(HandleServerCommand);
        _commandPublisher.Subscribe(HandleServerCommand);

        // Reuse an existing local game; only create it the first time this flow runs.
        // Re-creating would dispose a game that an overlapping initialization may still be using.
        if (_localGame != null)
            return;

        CreateAndInitializeLocalGame();
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
        if (HasJoinedPlayers) return; // Reject mode change while players are connected

        _hostMode = mode;
        NotifyPropertyChanged(nameof(IsLanMode));
        NotifyPropertyChanged(nameof(IsOnlineMode));
        ClearHostingState();
        CancelAndRestartServer().SafeFireAndForget(
            ex => _logger.LogError(ex, "Error restarting server after host mode change"));
    }

    /// <summary>
    /// Gets whether the host mode can still be changed. Once any player has joined,
    /// the lobby must not be re-created because it would disconnect the joined players.
    /// </summary>
    public bool CanChangeHostMode => !HasJoinedPlayers;

    private bool HasJoinedPlayers => _players.Any(p => p.Player.Status is PlayerStatus.Joined or PlayerStatus.Ready);

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
            if (RoomCode != null)
                return string.Format(_localizationService.GetString("Hosting_RoomReady"), RoomCode);
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

    private void ClearHostingState()
    {
        IsHosting = false;
        RoomCode = null;
        HostingError = null;
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
        }
        _initCts?.Dispose();
        _initCts = new CancellationTokenSource();

        try
        {
            IsHosting = true;
            try
            {
                await InitializeLobbyAndSubscribe(_initCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Cancelled by a superseded restart or by detach/dispose; treat as silent return.
                return;
            }
            if (_initCts.IsCancellationRequested || _isDisposed)
                return;
        }
        finally
        {
            IsHosting = false;
        }
    }

    private Task CopyRoomCode()
    {
        // Clipboard access belongs to the platform layer; a clipboard service can be wired here.
        return Task.CompletedTask;
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

        // Close the online relay room now that the lobby is transitioning to deployment;
        // LAN hosting has no relay room to close.
        if (IsOnlineMode)
        {
            await _gameManager.CloseOnlineRoom();
        }

        // Set BattleMap on GameManager/ServerGame (propagates to clients via the command system)
        _gameManager.SetBattleMap(map);

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
        _commandPublisher.Unsubscribe(HandleServerCommand);
        MapConfig.Dispose();
        GC.SuppressFinalize(this);
    }

    public override void DetachHandlers()
    {
        _initCts?.Cancel();
        base.DetachHandlers();
        _commandPublisher.Unsubscribe(HandleServerCommand);
    }

    public override void AttachHandlers()
    {
        ResetHostingState();
        base.AttachHandlers();
        if (HasJoinedPlayers) return; // Don't restart hosting while players are connected
        _initCts?.Cancel();
        _initCts?.Dispose();
        _initCts = new CancellationTokenSource();
        InitializeLobbyAndSubscribe(_initCts.Token).SafeFireAndForget(
            ex => _logger.LogError(ex, "Error initializing lobby"));
    }

    private void ResetHostingState()
    {
        if (_hostMode != HostMode.Lan)
        {
            _hostMode = HostMode.Lan;
            NotifyPropertyChanged(nameof(IsLanMode));
            NotifyPropertyChanged(nameof(IsOnlineMode));
        }
        ClearHostingState();
    }
}
