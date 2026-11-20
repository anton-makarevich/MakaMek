using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Presentation.ViewModels.Setup;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Presentation.ViewModels;

/// <summary>
/// The host half of the new-game setup screen. The hosting lifecycle lives in
/// <see cref="HostingSetupController"/> and <see cref="HostingLifecycleExtensions"/>; this screen
/// forwards their bound state and owns only the player-lobby template hooks.
/// </summary>
public class StartNewGameViewModel : NewGameViewModel, ISetupNotificationSink, IDisposable
{
    private readonly HostingSetupController _controller;
    private readonly SetupDependencies _deps;

    public StartNewGameViewModel(
        GameSetupSession session,
        SetupDependenciesFactory setupDependencies,
        MapConfigViewModel mapConfig,
        IImageService imageService,
        ILogger<StartNewGameViewModel> logger)
        : base(session, imageService, logger)
    {
        _deps = setupDependencies.Create(session, logger);

        _controller = new HostingSetupController(_deps, mapConfig, this);
        _controller.CommandStreamHandler = HandleServerCommand;
    }

    /// <summary>
    /// Gets whether multiplayer networking is currently enabled.
    /// </summary>
    public bool IsMultiplayerEnabled => _controller.IsMultiplayerEnabled;
    public void Notify(string? propertyName) => NotifyPropertyChanged(propertyName);

    /// <summary>
    /// Gets whether the host mode selector should be shown.
    /// </summary>
    public bool IsHostModeSelectorVisible => _controller.IsHostModeSelectorVisible;

    /// <summary>
    /// Gets whether the LAN connection details (server address) should be shown.
    /// </summary>
    public bool IsLanDetailsVisible => _controller.IsLanDetailsVisible;

    /// <summary>
    /// Gets whether the online connection details (hub, room code) should be shown.
    /// </summary>
    public bool IsOnlineDetailsVisible => _controller.IsOnlineDetailsVisible;

    /// <summary>
    /// Command to explicitly enable multiplayer for the currently selected host mode.
    /// </summary>
    public ICommand EnableMultiplayerCommand => _controller.EnableMultiplayerCommand;

    /// <summary>
    /// Gets the child ViewModel that tracks online connection status.
    /// </summary>
    public ConnectionStatusViewModel ConnectionStatus => _controller.ConnectionStatus;

    /// <summary>
    /// Gets whether the connection status banner should be shown for the hosted online session.
    /// </summary>
    public bool IsConnectionBannerVisible => _controller.IsConnectionBannerVisible;

    public MapConfigViewModel MapConfig => _controller.MapConfig;

    public bool CanStartGame => _controller.CanStartGame;

    /// <summary>
    /// Gets the server address if LAN is running
    /// </summary>
    public string ServerIpAddress => _deps.GetServerIpAddress();

    public bool CanStartLanServer => _controller.CanStartLanServer;

    /// <summary>
    /// Gets or sets whether the game is hosted over the local network.
    /// </summary>
    public bool IsLanMode
    {
        get => _controller.IsLanMode;
        set => _controller.IsLanMode = value;
    }

    /// <summary>
    /// Gets or sets whether the game is hosted through the cloud relay.
    /// </summary>
    public bool IsOnlineMode
    {
        get => _controller.IsOnlineMode;
        set => _controller.IsOnlineMode = value;
    }

    /// <summary>
    /// Gets whether the host mode can still be changed.
    /// </summary>
    public bool CanChangeHostMode => _controller.CanChangeHostMode;

    /// <summary>
    /// Gets the relay hub backing the online lobby, including its reachability state.
    /// </summary>
    public HubEntryViewModel? ActiveHub => _controller.ActiveHub;

    /// <summary>
    /// Gets the room code of the online lobby, if hosting online.
    /// </summary>
    public string? RoomCode => _controller.RoomCode;

    /// <summary>
    /// Gets the last hosting error, if any.
    /// </summary>
    public string? HostingError => _controller.HostingError;

    /// <summary>
    /// Gets the localized status text for the current hosting state.
    /// </summary>
    public string HostingStatusText => _controller.GetHostingStatusText(_deps);

    /// <summary>
    /// Gets or sets whether the last room-code copy attempt succeeded.
    /// </summary>
    public bool? RoomCodeCopySucceeded => _controller.RoomCodeCopySucceeded;

    /// <summary>
    /// Gets the localized status text for the last room-code copy attempt.
    /// </summary>
    public string CopyRoomCodeStatusText => _controller.GetCopyRoomCodeStatusText(_deps);

    /// <summary>
    /// Copies the room code to the clipboard (enabled while an online room exists).
    /// </summary>
    public ICommand CopyRoomCodeCommand => _controller.CopyRoomCodeCommand;

    public bool IsNetworkSectionExpanded
    {
        get => _controller.IsNetworkSectionExpanded;
        set => _controller.IsNetworkSectionExpanded = value;
    }

    public void ToggleNetworkSection() => _controller.ToggleNetworkSection();

    public ICommand StartGameCommand =>
        new AsyncCommand(() => _controller.StartGameAsync(NavigationService, _deps, MapConfig));

    public async Task InitializeLobbyAndSubscribe(CancellationToken cancellationToken)
    {
        await _controller.InitializeLobbyAndSubscribe(_deps, cancellationToken);
        NotifyPropertyChanged(nameof(IsMultiplayerEnabled));
        NotifyPropertyChanged(nameof(RoomCode));
        NotifyPropertyChanged(nameof(HostingError));
        NotifyPropertyChanged(nameof(ActiveHub));
    }

    /// <summary>
    /// Restarts hosting for the currently selected mode. No-op while any player has joined.
    /// </summary>
    public Task CancelAndRestartServer() => _controller.CancelAndRestartServerAsync(_deps);

    // Implementation of the abstract method from the base class
    protected override Task HandleCommandInternal(IGameCommand command) =>
        _controller.HandleCommandAsync(command);

    protected override void OnPlayersChanged() => NotifyPropertyChanged(nameof(CanStartGame));

    public override bool CanPublishCommands => !ConnectionStatus.IsConnectionDegraded;

    public void Dispose()
    {
        _controller.Dispose();
        GC.SuppressFinalize(this);
    }

    public override void DetachHandlers()
    {
        _controller.Detach(_deps);
        base.DetachHandlers();
    }

    public override void AttachHandlers()
    {
        _controller.SubscribeToMapChanges(_deps);

        if (_controller.HasJoinedPlayers)
        {
            base.AttachHandlers();
            return; // Don't reset hosting state or restart hosting while players are connected
        }

        _controller.ResetHostingState(_deps);
        base.AttachHandlers();
        _controller.RestartLocalLobbyInitialization(_deps);
    }
}



