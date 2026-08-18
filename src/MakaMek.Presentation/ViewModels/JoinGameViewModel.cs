using System.Text.RegularExpressions;
using System.Windows.Input;
using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.Transport.SignalR.Client.Relay;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.Models.Logger;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Presentation.ViewModels;

/// <summary>
/// Determines how the local player joins a game.
/// </summary>
public enum JoinMode
{
    Lan,
    Online
}

public class JoinGameViewModel : NewGameViewModel, IAsyncDisposable
{
    private readonly IGameConnector _gameConnector;
    private readonly ILocalizationService _localizationService;
    private readonly IClipboardService _clipboardService;
    private readonly IBattleMapFactory _mapFactory;
    private readonly IMapPreviewRenderer _mapPreviewRenderer;
    private CancellationTokenSource? _previewCts;
    private BattleMap? _previewMap;
    private object? _previewImage;
    private JoinMode _joinMode = JoinMode.Lan;
    private CancellationTokenSource? _activeJoinCts;
    private int _clipboardReadGeneration;

    private const string RoomCodeFormat = "^[A-HJ-NP-Z2-9]{6}$";

    public JoinGameViewModel(
        IUnitsLoader unitsLoader,
        ICommandPublisher commandPublisher,
        IDispatcherService dispatcherService,
        IGameFactory gameFactory,
        IGameConnector gameConnector,
        IFileCachingService cachingService,
        IBotManager botManager,
        ILogger<JoinGameViewModel> logger,
        IMechFactory mechFactory,
        IBattleMapFactory mapFactory,
        IMapPreviewRenderer mapPreviewRenderer,
        IClipboardService clipboardService,
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
        _gameConnector = gameConnector;
        _localizationService = localizationService ?? new FakeLocalizationService();
        _clipboardService = clipboardService;
        _mapFactory = mapFactory;
        _mapPreviewRenderer = mapPreviewRenderer;

        AddPlayerCommand = new AsyncCommand(() => AddPlayer());
        AddBotCommand = new AsyncCommand(()=>AddPlayer(controlType: PlayerControlType.Bot));
        ConnectCommand = new AsyncCommand(ConnectToServer, (_)=>CanConnect);
        JoinRoomCommand = new AsyncCommand(JoinRoom, (_) => CanJoin);
    }

    public override void AttachHandlers()
    {
        base.AttachHandlers();
        TryAutoFillRoomCodeFromClipboard().SafeFireAndForget(
            ex => _logger.LogError(ex, "Error reading room code from clipboard"));
    }

    /// <summary>
    /// The lobby map received from the server, shown as a read-only preview before the game starts.
    /// </summary>
    public BattleMap? PreviewMap => _previewMap;

    /// <summary>
    /// Rendered preview image for the lobby map received from the server.
    /// </summary>
    public object? PreviewImage => _previewImage;

    /// <summary>
    /// Whether a lobby map preview is currently available for display.
    /// </summary>
    public bool HasLobbyMapPreview => _previewMap != null;

    private async Task UpdateLobbyMapPreviewAsync(BattleMap? map)
    {
        _previewMap = map;
        NotifyPropertyChanged(nameof(PreviewMap));
        NotifyPropertyChanged(nameof(HasLobbyMapPreview));

        _previewCts?.Cancel();
        _previewCts?.Dispose();

        // Drop the previous preview image immediately: a null replacement, a cancelled
        // render, or a render that returns null must never leave a stale map's image on
        // screen. Notify before rendering so the UI clears while the new preview is built.
        (_previewImage as IDisposable)?.Dispose();
        _previewImage = null;
        NotifyPropertyChanged(nameof(PreviewImage));

        if (map == null)
        {
            _previewCts = null;
            return;
        }

        _previewCts = new CancellationTokenSource();
        var token = _previewCts.Token;

        try
        {
            var newPreview = await _mapPreviewRenderer.GeneratePreview(map, cancellationToken: token);
            if (!token.IsCancellationRequested && newPreview != null)
            {
                _previewImage = newPreview;
            }
            else
            {
                // Render was superseded or produced nothing; dispose the unused result.
                (newPreview as IDisposable)?.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Map preview generation cancelled - a newer map superseded this preview");
        }

        NotifyPropertyChanged(nameof(PreviewImage));
    }

    private async Task TryAutoFillRoomCodeFromClipboard()
    {
        var generation = _clipboardReadGeneration;
        var initialOnlineMode = IsOnlineMode;
        var initialRoomCode = RoomCode;
        var initialConnected = IsConnected;

        var clipboardText = await _clipboardService.GetText();
        if (string.IsNullOrEmpty(clipboardText)) return;

        var candidate = clipboardText.Trim().ToUpperInvariant();
        if (!Regex.IsMatch(candidate, RoomCodeFormat)) return;

        if (generation != _clipboardReadGeneration ||
            IsConnected != initialConnected ||
            IsOnlineMode != initialOnlineMode ||
            RoomCode != initialRoomCode) return;

        if (!string.IsNullOrEmpty(initialRoomCode)) return;

        IsOnlineMode = true;
        RoomCode = candidate;
    }

    private void InvalidatePendingClipboardRead()
    {
        _clipboardReadGeneration++;
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
                
            case SetBattleMapCommand setBattleMapCommand:
                // The ClientGame has already applied the map; show it as a lobby preview.
                // Navigation to the battle map happens when the game actually starts.
                var lobbyPreviewMap = _mapFactory.CreateFromData(setBattleMapCommand.MapData);
                await UpdateLobbyMapPreviewAsync(lobbyPreviewMap);
                break;

            case ChangePhaseCommand phaseCommand:
                // The game start signal: navigate to the battle map once the phase leaves Start
                if (phaseCommand.Phase == PhaseNames.Start) break;

                var battleMapViewModel = NavigationService.GetViewModel<BattleMapViewModel>();
                if (battleMapViewModel == null)
                {
                    throw new Exception("BattleMapViewModel is not registered");
                }
                battleMapViewModel.Game = _localGame;

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
            NotifyPropertyChanged(nameof(JoinedRoomInfoText));
        }
    } = string.Empty;

    public string ServerAddress => $"http://{ServerIp}:2439/makamekhub";

    /// <summary>
    /// Gets whether the client is currently connected, either over LAN or through the relay.
    /// </summary>
    public bool IsConnected => _gameConnector.IsConnected;

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
        NotifyPropertyChanged(nameof(IsLanFormVisible));
        NotifyPropertyChanged(nameof(IsOnlineFormVisible));
        NotifyPropertyChanged(nameof(JoinedRoomInfoText));
        _activeJoinCts?.Cancel();
        ClearJoinState();
    }

    public bool IsLanFormVisible => IsLanMode && !IsConnected;

    public bool IsOnlineFormVisible => IsOnlineMode && !IsConnected;

    /// <summary>
    /// Gets or sets the room code used to join an online game.
    /// </summary>
    public string RoomCode
    {
        get;
        set
        {
            var normalized = value.Trim().ToUpperInvariant();
            if (field == normalized) return;
            field = normalized;
            NotifyPropertyChanged();
            (JoinRoomCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(CanJoin));
            NotifyPropertyChanged(nameof(JoinedRoomInfoText));
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

    /// <summary>
    /// Gets the localized room info text when connected.
    /// </summary>
    public string JoinedRoomInfoText
    {
        get
        {
            if (!IsConnected)
                return string.Empty;

            return IsOnlineMode 
                ? string.Format(_localizationService.GetString("Join_RoomJoinedInfo"), RoomCode) 
                : string.Format(_localizationService.GetString("Join_ServerConnectedInfo"), ServerAddress);
        }
    }

    private void ClearJoinState()
    {
        IsJoining = false;
        JoinError = null;
    }

    private void RefreshConnectionState()
    {
        foreach (var player in _players)
        {
            player.RefreshStatus();
        }
        (ConnectCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (JoinRoomCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        NotifyPropertyChanged(nameof(IsConnected));
        NotifyPropertyChanged(nameof(IsLanFormVisible));
        NotifyPropertyChanged(nameof(IsOnlineFormVisible));
        NotifyPropertyChanged(nameof(CanConnect));
        NotifyPropertyChanged(nameof(CanJoin));
        NotifyPropertyChanged(nameof(CanAddPlayer));
        NotifyPropertyChanged(nameof(JoinedRoomInfoText));
    }

    private async Task JoinRoom()
    {
        if (!CanJoin) return;

        InvalidatePendingClipboardRead();

        IsJoining = true;
        JoinError = null;

        CancellationTokenSource? joinCts = null;
        try
        {
            joinCts = new CancellationTokenSource();
            _activeJoinCts = joinCts;

            // A new device joins without a session token; the Hub mints a device session.
            await _gameConnector.JoinOnline(RoomCode, sessionToken: null, joinCts.Token);
            if (_joinMode != JoinMode.Online)
            {
                // A newer join superseded this one; clean up any connection it left behind
                if (!ReferenceEquals(_activeJoinCts, joinCts))
                    await _gameConnector.Disconnect();
                return;
            }
            if (!_gameConnector.IsConnected)
            {
                JoinError = GetJoinErrorText(_gameConnector.OnlineError);
                return;
            }

            _commandPublisher.Subscribe(HandleServerCommand);
            CreateAndInitializeLocalGame(_gameConnector.ConnectedHostGameId);

            _localGame!.RequestLobbyStatus(new RequestGameLobbyStatusCommand
            {
                GameOriginId = _localGame.Id
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Online game join was cancelled");
        }
        catch (Exception ex)
        {
            _commandPublisher.Unsubscribe(HandleServerCommand);
            _logger.LogError(ex, "Error joining online game");
            await _gameConnector.Disconnect();
            JoinError = GetJoinErrorText(_gameConnector.OnlineError);
        }
        finally
        {
            // Only clear the token source we own; a newer join may have replaced it
            if (ReferenceEquals(_activeJoinCts, joinCts))
            {
                _activeJoinCts?.Dispose();
                _activeJoinCts = null;
            }
            IsJoining = false;
            RefreshConnectionState();
        }
    }

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

        InvalidatePendingClipboardRead();

        try
        {
            await _gameConnector.ConnectToLan(ServerAddress);
            if (!_gameConnector.IsConnected) return;

            _commandPublisher.Subscribe(HandleServerCommand);
            CreateAndInitializeLocalGame();

            _localGame!.Logger.LogAttemptedToConnectToServerIp(ServerIp);
            _localGame.RequestLobbyStatus(new RequestGameLobbyStatusCommand
            {
                GameOriginId = _localGame.Id
            });
            RefreshConnectionState();
        }
        catch (Exception ex)
        {
            _commandPublisher.Unsubscribe(HandleServerCommand);
            _localGame?.Logger.LogError(ex, "Error connecting to server: {Message}", ex.Message);
            await _gameConnector.Disconnect();
            RefreshConnectionState();
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
        await _gameConnector.Disconnect();
        RefreshConnectionState();
    }

    public async ValueTask DisposeAsync()
    {
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = null;
        (_previewImage as IDisposable)?.Dispose();
        _previewImage = null;
        await Disconnect();
        await _gameConnector.DisposeAsync();
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
