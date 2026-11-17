using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Bots.Services;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Players;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MakaMek.Services;
using Sanet.MVVM.Core.Services;

namespace Sanet.MakaMek.Presentation.ViewModels.Setup;

/// <summary>
/// The shared game-setup workflow behind <see cref="NewGameViewModel"/>: the player lobby, the
/// available-unit catalogue, the local client game, the default-player cache, the setup dialogs and
/// the join/ready commands. Keeping it here means the base view model stays a thin forwarding facade
/// for both the host and the join screens instead of owning every setup collaborator itself.
/// </summary>
public sealed class GameSetupSession
{
    private readonly IUnitsLoader _unitsLoader;
    private readonly IDispatcherService _dispatcherService;
    private readonly IGameFactory _gameFactory;
    private readonly IBotManager _botManager;
    private readonly DefaultPlayerStore _defaultPlayerStore;
    private readonly GameSetupDialogService _dialogs;
    private readonly LocalPlayerCommands _localPlayerCommands;
    private List<UnitData> _availableUnits = [];

    public GameSetupSession(
        IUnitsLoader unitsLoader,
        IDispatcherService dispatcherService,
        IGameFactory gameFactory,
        IFileCachingService cachingService,
        IBotManager botManager,
        IMechFactory mechFactory,
        ILogger logger)
    {
        _unitsLoader = unitsLoader;
        _dispatcherService = dispatcherService;
        _gameFactory = gameFactory;
        _botManager = botManager;
        _defaultPlayerStore = new DefaultPlayerStore(cachingService);
        _dialogs = new GameSetupDialogService(mechFactory);
        _localPlayerCommands = new LocalPlayerCommands(botManager);
    }

    /// <summary>
    /// The local player lobby. Shared with the view model so both the host and join screens mutate
    /// the same observable collection.
    /// </summary>
    public ObservableCollection<PlayerViewModel> Players { get; } = [];

    /// <summary>
    /// The local <see cref="ClientGame"/> mirrored by the setup screen, when one has been created.
    /// </summary>
    public ClientGame? LocalGame { get; set; }

    /// <summary>
    /// The last catalogue read, projected for the available-units dialog.
    /// </summary>
    public IReadOnlyList<UnitData> AvailableUnits => _availableUnits;

    /// <summary>
    /// Whether a player has joined or is ready. Such a lobby must not be re-created.
    /// </summary>
    public bool HasJoinedPlayers =>
        Players.Any(p => p.Player.Status is PlayerStatus.Joined or PlayerStatus.Ready);

    /// <summary>
    /// Whether every player has units and is ready. Connection health is the screen's concern.
    /// </summary>
    public bool AllPlayersReady =>
        Players.Count > 0 && Players.All(p => p.Units.Count > 0 && p.Player.Status == PlayerStatus.Ready);

    /// <summary>
    /// Runs a server command on the UI thread through the injected dispatcher.
    /// </summary>
    public void Dispatch(IGameCommand command, Func<IGameCommand, Task> handler) =>
        _dispatcherService.RunOnUIThread(async () => await handler(command));

    /// <summary>
    /// Reads the unit catalogue, replacing the previous snapshot.
    /// </summary>
    public async Task LoadAvailableUnitsAsync() =>
        _availableUnits = await _unitsLoader.LoadUnits();

    /// <summary>
    /// Adds a player to the lobby, building the view model through the derived screen's factory.
    /// </summary>
    public Task AddPlayerAsync(
        PlayerData? playerData,
        PlayerControlType controlType,
        Func<Player, bool, PlayerViewModel> createPlayerViewModel,
        bool canAddPlayer)
    {
        if (!canAddPlayer) return Task.CompletedTask;
        var isDefaultPlayer = playerData != null;

        // Generate a vibrant random tint for a fresh player.
        playerData ??= PlayerData.CreateDefault() with { Tint = PlayerTint.Generate() };

        var newPlayer = new Player(playerData.Value, controlType, Guid.NewGuid());
        Players.Add(createPlayerViewModel(newPlayer, isDefaultPlayer));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes a removable player from the lobby.
    /// </summary>
    public Task RemovePlayerAsync(PlayerViewModel? playerVm)
    {
        if (playerVm?.IsRemovable != true) return Task.CompletedTask;

        Players.Remove(playerVm);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds the cached/created default player when the screen allows it. Admission is evaluated again
    /// after the cache read because the lobby may have filled up while it was pending.
    /// </summary>
    /// <returns><c>true</c> when a player was actually added.</returns>
    public async Task<bool> AddDefaultPlayerAsync(
        Func<Player, bool, PlayerViewModel> createPlayerViewModel,
        Func<bool> canAddPlayer)
    {
        if (!canAddPlayer()) return false;

        var defaultPlayer = await _defaultPlayerStore.LoadOrCreateAsync(LocalGame?.Logger);
        if (!canAddPlayer()) return false;

        await AddPlayerAsync(defaultPlayer, PlayerControlType.Human, createPlayerViewModel, canAddPlayer: true);
        return true;
    }

    /// <summary>
    /// Persists the default player so the next launch reuses its name and tint.
    /// </summary>
    public Task SaveDefaultPlayerAsync(PlayerData playerData) =>
        _defaultPlayerStore.SaveAsync(playerData, LocalGame?.Logger);

    /// <summary>
    /// Publishes the local player's join (and registers its bot when it is one).
    /// </summary>
    public void PublishJoinCommand(PlayerViewModel playerVm)
    {
        if (LocalGame == null) return;

        _localPlayerCommands.PublishJoin(LocalGame, playerVm);
    }

    /// <summary>
    /// Publishes the local player's ready status.
    /// </summary>
    public void PublishSetReadyCommand(PlayerViewModel playerVm)
    {
        if (LocalGame == null) return;

        _localPlayerCommands.PublishSetReady(LocalGame, playerVm);
    }

    /// <summary>
    /// Creates the local <see cref="ClientGame"/> via <see cref="IGameFactory"/>, constructs a
    /// <see cref="DecisionEngineProvider"/> for it, and initializes the <see cref="IBotManager"/>.
    /// Any previously created local game is disposed first so the setup is safe to run repeatedly.
    /// </summary>
    public ClientGame CreateAndInitializeLocalGame(
        ICommandPublisher publisher,
        Guid? serverGameId = null)
    {
        if (LocalGame != null)
        {
            LocalGame.Dispose();
            LocalGame = null;
        }

        LocalGame = _gameFactory.CreateClientGame(publisher, serverGameId);
        _botManager.Initialize(LocalGame, new DecisionEngineProvider(LocalGame));
        return LocalGame;
    }

    /// <summary>
    /// Shows the available-units dialog and adds the picked unit to the player.
    /// </summary>
    public async Task ShowAvailableUnitsTableAsync(
        INavigationService navigationService,
        PlayerViewModel playerVm)
    {
        var selectedUnit = await _dialogs.ShowAvailableUnitsAsync(
            navigationService,
            _availableUnits,
            playerVm.CanAddUnit);
        if (selectedUnit.HasValue)
        {
            await playerVm.AddUnit(selectedUnit.Value);
        }
    }

    /// <summary>
    /// Shows the unit info/pilot-edit dialog.
    /// </summary>
    public Task<PilotEditResult?> ShowUnitInfoAsync(
        INavigationService navigationService,
        UnitData unitData,
        PilotData? pilotData,
        bool canEdit) =>
        _dialogs.ShowUnitInfoAsync(navigationService, unitData, pilotData, canEdit);
}

/// <summary>
/// Generates the vibrant random tint used for a fresh player.
/// </summary>
public static class PlayerTint
{
    public static string Generate()
    {
        // Use HSV color space to ensure vibrant, colorful colors
        // Hue: 0-360 (full range for variety)
        // Saturation: 0.6-1.0 (avoid grayish colors)
        // Value/Brightness: 0.4-1.0 (avoid very dark colors but allow some darker ones for variety)
        var random = Random.Shared;

        var hue = random.NextDouble() * 360.0;
        var saturation = 0.6 + (random.NextDouble() * 0.4); // 0.6-1.0
        var value = 0.4 + (random.NextDouble() * 0.6); // 0.4-1.0

        var rgb = HsvToRgb(hue, saturation, value);
        return $"#{rgb.Red:X2}{rgb.Green:X2}{rgb.Blue:X2}";
    }

    private static (int Red, int Green, int Blue) HsvToRgb(double hue, double saturation, double value)
    {
        var c = value * saturation;
        var x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
        var m = value - c;

        var rgbValues = hue switch
        {
            >= 0 and < 60 => (c, x, 0.0),
            >= 60 and < 120 => (x, c, 0.0),
            >= 120 and < 180 => (0.0, c, x),
            >= 180 and < 240 => (0.0, x, c),
            >= 240 and < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };

        return (
            (int)Math.Round((rgbValues.Item1 + m) * 255),
            (int)Math.Round((rgbValues.Item2 + m) * 255),
            (int)Math.Round((rgbValues.Item3 + m) * 255)
        );
    }
}

