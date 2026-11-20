using System.Globalization;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Sanet.Localization;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels.Setup;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Presentation.ViewModels;

/// <summary>
/// The join half of the new-game setup screen. The transport lifecycle lives in
/// <see cref="JoinSetupController"/> and <see cref="JoinLifecycleExtensions"/>; this screen forwards
/// their bound state, owns the commands and refreshes them when the controller reports a connection
/// transition.
/// </summary>
public class JoinGameViewModel : NewGameViewModel, ISetupNotificationSink, IAsyncDisposable
{
    private readonly JoinSetupController _controller;
    private readonly ILocalizationService _localizationService;

    public JoinGameViewModel(
        GameSetupSession session,
        SetupDependenciesFactory setupDependencies,
        ILocalizationService localizationService,
        IImageService imageService,
        ILogger<JoinGameViewModel> logger)
        : base(session, imageService, logger)
    {
        _localizationService = localizationService;
        var deps = setupDependencies.Create(session, logger);
        _controller = new JoinSetupController(deps, this);
        _controller.NavigationServiceAccessor = () => NavigationService;
        _controller.CommandStreamHandler = HandleServerCommand;
        _controller.ServerIpAccessor = () => ServerIp;
        _controller.ServerAddressAccessor = () => ServerAddress;
        _controller.RoomCodeAccessor = () => RoomCode;
        _controller.CanConnectAccessor = () => CanConnect;
        _controller.CanJoinAccessor = () => CanJoin;
    }

    public void Notify(string? propertyName)
    {
        // A connection transition also refreshes the commands and the player statuses.
        if (propertyName == nameof(IsConnected)) RefreshConnectionState();
        NotifyPropertyChanged(propertyName);
    }

    public override void AttachHandlers()
    {
        base.AttachHandlers();
        RunInBackground(TryAutoFillRoomCodeFromClipboard, "Error reading room code from clipboard");
    }

    /// <summary>
    /// The lobby map received from the server, shown as a read-only preview before the game starts.
    /// </summary>
    public BattleMap? PreviewMap => _controller.PreviewMap;

    /// <summary>
    /// Rendered preview image for the lobby map received from the server.
    /// </summary>
    public object? PreviewImage => _controller.PreviewImage;

    /// <summary>
    /// Whether a lobby map preview is currently available for display.
    /// </summary>
    public bool HasLobbyMapPreview => _controller.HasLobbyMapPreview;

    private async Task TryAutoFillRoomCodeFromClipboard()
    {
        var generation = _controller.ClipboardReadGeneration;
        var initialOnlineMode = IsOnlineMode;
        var initialRoomCode = RoomCode;
        var initialConnected = IsConnected;

        var candidate = await _controller.ReadClipboardRoomCodeAsync();
        if (candidate is null) return;

        if (generation != _controller.ClipboardReadGeneration ||
            IsConnected != initialConnected ||
            IsOnlineMode != initialOnlineMode ||
            RoomCode != initialRoomCode) return;

        if (!string.IsNullOrEmpty(initialRoomCode)) return;

        IsOnlineMode = true;
        RoomCode = candidate;
    }

    // Implementation of the abstract method from a base class
    protected override async Task HandleCommandInternal(IGameCommand command) =>
        await _controller.HandleLobbyCommandAsync(command);

    public string ServerIp
    {
        get;
        set
        {
            SetProperty(ref field, value);
            _controller.RefreshCommands();
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
    public bool IsConnected => _controller.IsConnected;

    public ICommand ConnectCommand => _controller.ConnectCommand;

    public bool CanConnect => !string.IsNullOrWhiteSpace(ServerIp) && !IsConnected;

    /// <summary>
    /// Gets or sets whether the join uses the local network.
    /// </summary>
    public bool IsLanMode
    {
        get => _controller.IsLanMode;
        set => _controller.IsLanMode = value;
    }

    /// <summary>
    /// Gets or sets whether the join uses the cloud relay.
    /// </summary>
    public bool IsOnlineMode
    {
        get => _controller.IsOnlineMode;
        set => _controller.IsOnlineMode = value;
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
            _controller.RefreshCommands();
            NotifyPropertyChanged(nameof(CanJoin));
            NotifyPropertyChanged(nameof(JoinedRoomInfoText));
        }
    } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether an online join can be attempted.
    /// </summary>
    public bool CanJoin => !_controller.IsJoining && !string.IsNullOrWhiteSpace(RoomCode) && !IsConnected;

    /// <summary>
    /// Joins an online game by room code through the cloud relay.
    /// </summary>
    public ICommand JoinRoomCommand => _controller.JoinRoomCommand;

    /// <summary>
    /// Gets the last online join error, if any.
    /// </summary>
    public string? JoinError => _controller.JoinError;

    /// <summary>
    /// Gets the localized status text for the current online join state.
    /// </summary>
    public string JoinStatusText => _controller.JoinStatusText;

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
                ? string.Format(CultureInfo.InvariantCulture,
                    _localizationService.GetString("Join_RoomJoinedInfo"), RoomCode)
                : string.Format(CultureInfo.InvariantCulture,
                    _localizationService.GetString("Join_ServerConnectedInfo"), ServerAddress);
        }
    }

    /// <summary>
    /// Gets the child ViewModel that tracks online connection status.
    /// </summary>
    public ConnectionStatusViewModel ConnectionStatus => _controller.ConnectionStatus;

    /// <summary>
    /// Gets whether the connection status banner should be shown while joined to a game.
    /// </summary>
    public bool IsConnectionBannerVisible => IsConnected && ConnectionStatus.IsConnectionDegraded;

    /// <summary>
    /// Gets whether the connection banner should offer a retry action.
    /// </summary>
    public bool CanRetryConnection => _controller.CanRetryConnection;

    /// <summary>
    /// Retries the most recent connection without discarding its endpoint or session.
    /// </summary>
    public ICommand RetryConnectionCommand => _controller.RetryConnectionCommand;

    private void RefreshConnectionState()
    {
        foreach (var player in PlayerSnapshot)
        {
            player.RefreshStatus();
        }
        _controller.RefreshCommands();
        NotifyPropertyChanged(nameof(IsConnected));
        NotifyPropertyChanged(nameof(IsLanFormVisible));
        NotifyPropertyChanged(nameof(IsOnlineFormVisible));
        NotifyPropertyChanged(nameof(CanConnect));
        NotifyPropertyChanged(nameof(CanJoin));
        NotifyPropertyChanged(nameof(CanAddPlayer));
        NotifyPropertyChanged(nameof(JoinedRoomInfoText));
        NotifyPropertyChanged(nameof(CanPublishCommands));
        NotifyPropertyChanged(nameof(IsConnectionBannerVisible));
        NotifyPropertyChanged(nameof(CanRetryConnection));
    }

    public Task Disconnect() => _controller.DisconnectAsync();

    public async ValueTask DisposeAsync()
    {
        await _controller.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    // Allow adding the default player before connection, but require connection for additional players
    protected override bool IsConnectedReady => IsConnected;

    // Implementation of abstract property from base class
    public override bool CanPublishCommands => IsConnected && !ConnectionStatus.IsConnectionDegraded;
}




