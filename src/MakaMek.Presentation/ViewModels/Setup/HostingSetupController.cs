using System.ComponentModel;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.ViewModels.Setup;

/// <summary>
/// Determines how the local player hosts a new game.
/// </summary>
public enum HostMode
{
    Lan,
    Online
}

/// <summary>
/// Owns the hosting state of the new-game setup screen (room code, hosting error, active hub,
/// multiplayer and host-mode flags) and forwards its notifications to the screen. The transport-heavy
/// lifecycle lives in <see cref="HostingLifecycleExtensions"/>; the screen only binds this state.
/// </summary>
public sealed class HostingSetupController : SetupState, IDisposable
{
    private readonly SetupDependencies _deps;
    private readonly MapConfigViewModel _mapConfig;
    private CancellationTokenSource? _initCts;
    private bool _isDisposed;
    private HostMode _hostMode = HostMode.Lan;

    public HostingSetupController(
        SetupDependencies deps,
        MapConfigViewModel mapConfig,
        ISetupNotificationSink owner)
        : base(owner)
    {
        _deps = deps;
        _mapConfig = mapConfig;
        ConnectionStatus = new ConnectionStatusViewModel(null, deps.DispatcherService.Scheduler, deps.Logger);
        ConnectionStatus.PropertyChanged += OnConnectionStatusPropertyChanged;

        CopyRoomCodeCommand = new AsyncCommand(CopyRoomCode, _ => RoomCode != null);
        EnableMultiplayerCommand = new AsyncCommand(ExecuteEnableMultiplayer,
            _ => !IsMultiplayerEnabled && !HasJoinedPlayers);
    }

    public MapConfigViewModel MapConfig => _mapConfig;

    public ConnectionStatusViewModel ConnectionStatus { get; }

    /// <summary>
    /// The screen's command-stream handler. Subscriptions must use this exact delegate so the screen
    /// can subscribe and unsubscribe the same method group.
    /// </summary>
    public Action<IGameCommand>? CommandStreamHandler { get; set; }

    public ICommand CopyRoomCodeCommand { get; }

    public ICommand EnableMultiplayerCommand { get; }

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
            NotifyPropertyChanged(nameof(StartNewGameViewModel.HostingStatusText));
            NotifyPropertyChanged(nameof(CanChangeHostMode));
            NotifyPropertyChanged(nameof(IsLanDetailsVisible));
            NotifyPropertyChanged(nameof(IsConnectionBannerVisible));
        }
    }

    /// <summary>
    /// Gets whether the host mode selector should be shown. Only shown when both
    /// hosting options (LAN and online) are available; platforms without LAN
    /// support skip straight to the online flow.
    /// </summary>
    public bool IsHostModeSelectorVisible => CanStartLanServer;

    /// <summary>
    /// Gets whether the LAN connection details (server address) should be shown.
    /// </summary>
    public bool IsLanDetailsVisible => IsMultiplayerEnabled && IsLanMode;

    /// <summary>
    /// Gets whether the online connection details (hub, room code) should be shown.
    /// </summary>
    public bool IsOnlineDetailsVisible => IsOnlineMode && RoomCode != null;

    /// <summary>
    /// Gets whether the connection status banner should be shown for the hosted online session.
    /// </summary>
    public bool IsConnectionBannerVisible => IsMultiplayerEnabled && ConnectionStatus.IsConnectionDegraded;

    public bool CanStartGame => _deps.Session.AllPlayersReady && !ConnectionStatus.IsConnectionDegraded;

    public bool IsNetworkSectionExpanded
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool CanStartLanServer => _deps.GameManager.CanStartLanServer;

    /// <summary>
    /// The current host mode, exposed for the status text.
    /// </summary>
    public HostMode CurrentHostMode => _hostMode;

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

    /// <summary>
    /// Gets whether the host mode can still be changed. Once any player has joined,
    /// the lobby must not be re-created because it would disconnect the joined players.
    /// The mode is also locked while multiplayer hosting is active, since switching
    /// would tear down the live transport.
    /// </summary>
    public bool CanChangeHostMode => !HasJoinedPlayers && !IsMultiplayerEnabled && !IsHosting;

    /// <summary>
    /// Gets the relay hub backing the online lobby, including its reachability state.
    /// Null while hosting over LAN.
    /// </summary>
    public HubEntryViewModel? ActiveHub
    {
        get;
        internal set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Gets the room code of the online lobby, if hosting online.
    /// </summary>
    public string? RoomCode
    {
        get;
        internal set
        {
            if (field == value) return; // Reject no-op when unchanged
            field = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(IsOnlineDetailsVisible));
            NotifyPropertyChanged(nameof(StartNewGameViewModel.HostingStatusText));
            (CopyRoomCodeCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Gets the last hosting error, if any.
    /// </summary>
    public string? HostingError
    {
        get;
        internal set
        {
            if (field == value) return; // Reject no-op when unchanged
            field = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(StartNewGameViewModel.HostingStatusText));
        }
    }

    /// <summary>
    /// Gets or sets whether the last room-code copy attempt succeeded. Null before the first attempt.
    /// </summary>
    public bool? RoomCodeCopySucceeded
    {
        get;
        internal set
        {
            if (field == value) return; // Reject no-op when unchanged
            field = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(StartNewGameViewModel.CopyRoomCodeStatusText));
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
            NotifyPropertyChanged(nameof(StartNewGameViewModel.HostingStatusText));
        }
    }

    /// <summary>
    /// Whether a player has joined or is ready. Such a lobby must not be re-created.
    /// </summary>
    public bool HasJoinedPlayers => _deps.Session.HasJoinedPlayers;

    internal CancellationTokenSource? InitCts
    {
        get => _initCts;
        set => _initCts = value;
    }

    internal bool IsDisposed => _isDisposed;

    internal bool IsHostingActive
    {
        get => IsHosting;
        set => IsHosting = value;
    }

    internal void ApplyHostMode(HostMode mode) => SetHostMode(mode);

    internal void SetMultiplayerEnabled(bool value) => IsMultiplayerEnabled = value;

    internal HostingMapBroadcaster? Broadcaster { get; set; }

    internal void NotifyChanged(string? propertyName) => NotifyPropertyChanged(propertyName);

    internal void ClearHostingState()
    {
        IsHosting = false;
        ActiveHub = null;
        RoomCode = null;
        HostingError = null;
        RoomCodeCopySucceeded = null;
    }

    public void ToggleNetworkSection() => IsNetworkSectionExpanded = !IsNetworkSectionExpanded;

    public Task HandleCommandAsync(IGameCommand command) =>
        new LobbyCommandHandler(_deps.GameManager, _deps.Session)
            .HandleAsync(command, name => NotifyChanged(name));

    private Task ExecuteEnableMultiplayer() => this.CancelAndRestartServerAsync(_deps);

    private async Task CopyRoomCode()
    {
        if (RoomCode == null) return;
        RoomCodeCopySucceeded = await _deps.ClipboardService.SetText(RoomCode);
    }

    private void SetHostMode(HostMode mode)
    {
        if (_hostMode == mode) return; // Reject no-op when unchanged
        if (!CanChangeHostMode) return; // Reject while players are connected or hosting is active

        _hostMode = mode;
        NotifyPropertyChanged(nameof(IsLanMode));
        NotifyPropertyChanged(nameof(IsOnlineMode));
        NotifyPropertyChanged(nameof(IsLanDetailsVisible));
        NotifyPropertyChanged(nameof(IsOnlineDetailsVisible));
        // Stale hosting display state from the previous mode must not leak into the new one
        ClearHostingState();
    }

    private void OnConnectionStatusPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConnectionStatusViewModel.IsConnectionDegraded))
        {
            NotifyPropertyChanged(nameof(IsConnectionBannerVisible));
            NotifyPropertyChanged(nameof(CanStartGame));
            NotifyPropertyChanged(nameof(NewGameViewModel.CanPublishCommands));
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
        _initCts?.Cancel();
        _initCts?.Dispose();
        _initCts = null;
        if (CommandStreamHandler is { } handler) _deps.CommandPublisher.Unsubscribe(handler);
        this.UnsubscribeFromOnlineStatus(_deps);
        Broadcaster?.Dispose();
        ConnectionStatus.PropertyChanged -= OnConnectionStatusPropertyChanged;
        ConnectionStatus.Dispose();
        MapConfig.Dispose();
    }
}



