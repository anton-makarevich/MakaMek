using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Map.Models;
using Sanet.MVVM.Core.Services;

namespace Sanet.MakaMek.Presentation.ViewModels.Setup;

/// <summary>
/// Determines how the local player joins a game.
/// </summary>
public enum JoinMode
{
    Lan,
    Online
}

/// <summary>
/// Owns the join screen's observable state (join mode, in-flight join, join error, lobby preview) and
/// its connection commands. The transport workflow lives in <see cref="JoinLifecycleExtensions"/>; the
/// screen only binds this state and forwards its connection transitions.
/// </summary>
public sealed class JoinSetupController : SetupState, IAsyncDisposable
{
    private readonly SetupDependencies _deps;
    private readonly RoomCodeClipboardReader _clipboardReader;
    private readonly LobbyMapPreview _preview;
    private JoinMode _joinMode = JoinMode.Lan;
    private CancellationTokenSource? _activeJoinCts;
    private string? _joinError;

    public JoinSetupController(
        SetupDependencies deps,
        ISetupNotificationSink owner)
        : base(owner)
    {
        _deps = deps;
        ConnectionStatus = new ConnectionStatusViewModel(null, deps.DispatcherService.Scheduler);
        _clipboardReader = new RoomCodeClipboardReader(deps.ClipboardService);
        _preview = new LobbyMapPreview(deps.MapPreviewRenderer, deps.Logger);
        ConnectionStatus.PropertyChanged += OnConnectionStatusPropertyChanged;

        ConnectCommand = new AsyncCommand(ConnectToServerAsync, _ => CanConnectAccessor?.Invoke() == true);
        JoinRoomCommand = new AsyncCommand(JoinRoomAsync, _ => CanJoinAccessor?.Invoke() == true);
        RetryConnectionCommand = new AsyncCommand(RetryConnectionAsync, _ => CanRetryConnection);
    }

    public ConnectionStatusViewModel ConnectionStatus { get; }

    /// <summary>
    /// The screen's command-stream handler. Subscriptions must use this exact delegate so the screen
    /// can subscribe and unsubscribe the same method group.
    /// </summary>
    public Action<IGameCommand>? CommandStreamHandler { get; set; }

    /// <summary>
    /// Supplies the screen's navigation service at command-dispatch time.
    /// </summary>
    public Func<INavigationService?>? NavigationServiceAccessor { get; set; }

    /// <summary>
    /// Supplies the screen's endpoint and room state to the connection commands.
    /// </summary>
    public Func<string>? ServerIpAccessor { get; set; }
    public Func<string>? ServerAddressAccessor { get; set; }
    public Func<string>? RoomCodeAccessor { get; set; }
    public Func<bool>? CanConnectAccessor { get; set; }
    public Func<bool>? CanJoinAccessor { get; set; }

    public ICommand ConnectCommand { get; }
    public ICommand JoinRoomCommand { get; }
    public ICommand RetryConnectionCommand { get; }

    public bool IsConnected => _deps.GameConnector.IsConnected;

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

    /// <summary>
    /// Whether an online join or a reconnect is currently in flight.
    /// </summary>
    public bool IsJoining
    {
        get;
        internal set
        {
            if (field == value) return; // Reject no-op when unchanged
            field = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(JoinStatusText));
            (JoinRoomCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(JoinGameViewModel.CanJoin));
        }
    }

    /// <summary>
    /// Gets the last online join error, if any.
    /// </summary>
    public string? JoinError
    {
        get => _joinError;
        internal set
        {
            if (_joinError == value) return; // Reject no-op when unchanged
            _joinError = value;
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
                return _deps.LocalizationService.GetString("Join_Connecting");
            if (JoinError != null)
                return JoinError;
            return string.Empty;
        }
    }

    /// <summary>
    /// The lobby map received from the server, shown as a read-only preview before the game starts.
    /// </summary>
    public BattleMap? PreviewMap => _preview.Map;

    /// <summary>
    /// Rendered preview image for the lobby map received from the server.
    /// </summary>
    public object? PreviewImage => _preview.Image;

    /// <summary>
    /// Whether a lobby map preview is currently available for display.
    /// </summary>
    public bool HasLobbyMapPreview => _preview.HasMap;

    public bool CanRetryConnection => !IsJoining
                                      && _deps.GameConnector.CanReconnect
                                      && ConnectionStatus.IsConnectionDegraded;

    internal LobbyMapPreview Preview => _preview;

    internal CancellationTokenSource? ActiveJoinCts
    {
        get => _activeJoinCts;
        set => _activeJoinCts = value;
    }

    public int ClipboardReadGeneration => _clipboardReader.Generation;

    public void InvalidatePendingClipboardRead() => _clipboardReader.InvalidatePendingRead();

    public Task<string?> ReadClipboardRoomCodeAsync() => _clipboardReader.ReadAsync();

    /// <summary>
    /// Re-evaluates the connection commands' availability after the screen state changed.
    /// </summary>
    public void RefreshCommands()
    {
        (ConnectCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (JoinRoomCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (RetryConnectionCommand as AsyncCommand)?.RaiseCanExecuteChanged();
    }

    internal void RaiseConnectionStateChanged() => NotifyExternal(nameof(JoinGameViewModel.IsConnected));

    internal void NotifyExternal(string? propertyName) => NotifyPropertyChanged(propertyName);

    public Task HandleLobbyCommandAsync(IGameCommand command) =>
        this.HandleLobbyCommand(command, _deps);

    private Task ConnectToServerAsync() => this.ConnectToServer(_deps);

    private Task JoinRoomAsync() => this.JoinRoom(_deps);

    private Task RetryConnectionAsync() => this.RetryConnection(_deps);

    public Task DisconnectAsync() => this.Disconnect(_deps);

    private void SetJoinMode(JoinMode mode)
    {
        if (_joinMode == mode) return; // Reject no-op when unchanged

        _joinMode = mode;
        NotifyPropertyChanged(nameof(IsLanMode));
        NotifyPropertyChanged(nameof(IsOnlineMode));
        NotifyPropertyChanged(nameof(JoinGameViewModel.IsLanFormVisible));
        NotifyPropertyChanged(nameof(JoinGameViewModel.IsOnlineFormVisible));
        NotifyPropertyChanged(nameof(JoinGameViewModel.JoinedRoomInfoText));
        _activeJoinCts?.Cancel();
        IsJoining = false;
        JoinError = null;
    }

    private void OnConnectionStatusPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConnectionStatusViewModel.IsConnectionDegraded))
        {
            NotifyPropertyChanged(nameof(NewGameViewModel.CanPublishCommands));
            NotifyPropertyChanged(nameof(CanRetryConnection));
            RaiseConnectionStateChanged();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _preview.Dispose();
        ConnectionStatus.PropertyChanged -= OnConnectionStatusPropertyChanged;
        ConnectionStatus.Dispose();
        await this.Disconnect(_deps);
        await _deps.GameConnector.DisposeAsync();
    }
}





