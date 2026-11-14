using System.Collections.ObjectModel;
using System.Windows.Input;
using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Players;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Presentation.ViewModels.Setup;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MakaMek.Services;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

/// <summary>
/// Shared setup screen: the player lobby, the available-unit catalogue, the local client game and the
/// join/ready commands. The workflow lives in <see cref="GameSetupSession"/>; this type forwards the
/// derived screens' template hooks to it so the host and join view models only add their own
/// transport-specific behaviour.
/// </summary>
public abstract class NewGameViewModel : BaseViewModel
{
    private readonly GameSetupSession _session;
    protected readonly ICommandPublisher _commandPublisher;
    private readonly IDispatcherService _dispatcherService;
    protected readonly ILogger _logger;

    /// <summary>
    /// The local player lobby, shared with the setup session.
    /// </summary>
    protected ObservableCollection<PlayerViewModel> _players => _session.Players;

    /// <summary>
    /// The local client game mirrored by the setup screen, or <c>null</c> before it is created.
    /// </summary>
    protected ClientGame? _localGame
    {
        get => _session.LocalGame;
        set => _session.LocalGame = value;
    }

    public ICommand? AddPlayerCommand { get; protected set; }
    public ICommand? AddBotCommand { get; protected set; }
    public ICommand RemovePlayerCommand { get; }

    /// <summary>
    /// Creates the setup screen. The trailing <paramref name="session"/> is resolved by the composition
    /// root; the per-source parameters are kept so existing construction sites stay source-compatible
    /// and are only used to build the session when it is not injected.
    /// </summary>
    protected NewGameViewModel(IUnitsLoader unitsLoader,
        ICommandPublisher commandPublisher,
        IDispatcherService dispatcherService,
        IGameFactory gameFactory,
        IFileCachingService cachingService,
        IBotManager botManager,
        IMechFactory mechFactory,
        ILogger logger,
        IImageService imageService,
        GameSetupSession? session = null)
    {
        _commandPublisher = commandPublisher;
        _dispatcherService = dispatcherService;
        _logger = logger;
        _session = session
            ?? new GameSetupSession(
                unitsLoader,
                dispatcherService,
                gameFactory,
                cachingService,
                botManager,
                mechFactory,
                logger);
        ImageService = imageService;

        RemovePlayerCommand = SetupCommands.CreateRemovePlayerCommand(RemovePlayer);
        AddPlayerCommand = SetupCommands.CreatePlayerCommand(() => AddPlayer());
        AddBotCommand = SetupCommands.CreatePlayerCommand(() => AddPlayer(controlType: PlayerControlType.Bot));
    }

    protected IDispatcherService DispatcherService => _dispatcherService;

    /// <summary>
    /// The number of players currently in the lobby.
    /// </summary>
    protected int PlayerCount => _players.Count;

    /// <summary>
    /// A materialised snapshot of the lobby players, safe to iterate without observing the
    /// collection type.
    /// </summary>
    protected PlayerViewModel[] PlayerSnapshot => [.. _players];

    /// <summary>
    /// Runs a fire-and-forget operation, logging a failure through the screen logger.
    /// </summary>
    protected void RunInBackground(Func<Task> operation, string errorMessage) =>
        operation().SafeFireAndForget(ex => _logger.LogError(ex, errorMessage));

    /// <summary>
    /// Image service the lobby unit cards load their preview from. Exposed so the views can
    /// hand it to the controls they declare; the platform-specific cast stays in the view.
    /// </summary>
    public IImageService ImageService { get; }

    // Common command handlers with a template method pattern
    internal void HandleServerCommand(IGameCommand command) =>
        _session.Dispatch(command, HandleCommandInternal);

    // Template method to be implemented by derived classes
    protected abstract Task HandleCommandInternal(IGameCommand command);

    /// <summary>
    /// Creates the local <see cref="ClientGame"/> through the setup session, which also constructs a
    /// <see cref="DecisionEngineProvider"/> for it and initializes the bot manager. Any previously
    /// created local game is disposed first so the setup is safe to run repeatedly.
    /// </summary>
    protected void CreateAndInitializeLocalGame(Guid? serverGameId = null, ICommandPublisher? publisherOverride = null) =>
        _session.CreateAndInitializeLocalGame(publisherOverride ?? _commandPublisher, serverGameId);

    /// <summary>
    /// Publishes the local player's join when this screen may publish commands.
    /// </summary>
    protected void PublishJoinCommand(PlayerViewModel playerVm)
    {
        if (!playerVm.IsLocalPlayer || !CanPublishCommands) return;

        _session.PublishJoinCommand(playerVm);
    }

    /// <summary>
    /// Publishes the local player's ready status when this screen may publish commands.
    /// </summary>
    protected void PublishSetReadyCommand(PlayerViewModel playerVm)
    {
        if (!playerVm.IsLocalPlayer || !CanPublishCommands) return;

        _session.PublishSetReadyCommand(playerVm);
    }

    /// <summary>
    /// Resolves the local player's data, falling back to a fresh default player.
    /// </summary>
    protected PlayerData GetLocalPlayerData() => _session.GetLocalPlayerData();

    // Common properties
    public ObservableCollection<PlayerViewModel> Players => _players;

    internal ClientGame? LocalGame => _localGame;

    /// <summary>
    /// The shared setup session, used by the derived screens to wire their coordinators.
    /// </summary>
    protected GameSetupSession Session => _session;

    // Abstract/virtual properties to be implemented by derived classes
    public abstract bool CanPublishCommands { get; }
    public virtual bool CanAddPlayer => (IsConnectedReady || PlayerCount == 0) && PlayerCount < 4;

    /// <summary>
    /// Whether the screen is connected enough to add another player.
    /// </summary>
    protected virtual bool IsConnectedReady => true;

    // Common player creation logic with a template method pattern
    protected virtual Task AddPlayer(PlayerData? playerData = null, PlayerControlType controlType = PlayerControlType.Human)
    {
        var result = _session.AddPlayerAsync(playerData, controlType, CreatePlayerViewModel, CanAddPlayer);
        NotifyPropertyChanged(nameof(CanAddPlayer));
        OnPlayersChanged();
        return result;
    }

    /// <summary>
    /// Hook for derived screens that expose player-dependent state.
    /// </summary>
    protected virtual void OnPlayersChanged()
    {
    }

    // Template method for creating player view models with appropriate callbacks
    protected virtual PlayerViewModel CreatePlayerViewModel(Player player, bool isDefaultPlayer = false) =>
        new(
            player,
            isLocalPlayer: true,
            PublishJoinCommand,
            PublishSetReadyCommand,
            ShowAvailableUnitsTable,
            ShowUnitInfo,
            OnPlayersChanged,
            isDefaultPlayer
                ? OnDefaultPlayerNameChanged
                : null,
            isDefaultPlayer,
            () => IsConnectedReady);

    /// <summary>
    /// Removes a player from the game setup
    /// </summary>
    /// <param name="playerVm">The player to remove</param>
    /// <returns>Task representing the async operation</returns>
    protected virtual Task RemovePlayer(PlayerViewModel? playerVm)
    {
        var result = _session.RemovePlayerAsync(playerVm);
        NotifyPropertyChanged(nameof(CanAddPlayer));
        OnPlayersChanged();
        return result;
    }

    public override void AttachHandlers()
    {
        base.AttachHandlers();
        _session.LoadAvailableUnitsAsync().SafeFireAndForget(ex => _logger.LogError(ex, "Error loading available units"));

        // Auto-add default player
        if (Players.Count == 0)
            AddDefaultPlayer().SafeFireAndForget(
                ex => _logger.LogError(ex, "Error adding default player")
                );
    }

    /// <summary>
    /// Adds the default player automatically when the view loads
    /// </summary>
    private Task AddDefaultPlayer() =>
        _session.AddDefaultPlayerAsync(CreatePlayerViewModel, CanAddPlayer);

    /// <summary>
    /// Handles player name changes for the default player
    /// </summary>
    protected Task OnDefaultPlayerNameChanged(Player updatedPlayer) =>
        _session.SaveDefaultPlayerAsync(updatedPlayer.ToData());

    public IReadOnlyList<UnitData> AvailableUnits => _session.AvailableUnits;

    protected Task ShowAvailableUnitsTable(PlayerViewModel playerVm) =>
        _session.ShowAvailableUnitsTableAsync(NavigationService, playerVm);

    protected Task<PilotEditResult?> ShowUnitInfo(UnitData unitData, PilotData? pilotData,
        bool canEdit = false) =>
        _session.ShowUnitInfoAsync(NavigationService, unitData, pilotData, canEdit);
}


