using Microsoft.Extensions.Logging;
using System.Text.Json;
using AsyncAwaitBestPractices.MVVM;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Players;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.Transport.SignalR.Client.Relay;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Map.Services;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MakaMek.Services;
using Sanet.MVVM.Core.Services;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class StartNewGameViewModelTests
{
    private readonly StartNewGameViewModel _sut;
    private readonly INavigationService _navigationService;
    private readonly BattleMapViewModel _battleMapViewModel;
    private readonly IGameManager _gameManager = Substitute.For<IGameManager>();
    private readonly ICommandPublisher _commandPublisher = Substitute.For<ICommandPublisher>();
    private readonly ClientGame _clientGame;
    private readonly ILogger<ClientGame> _logger = Substitute.For<ILogger<ClientGame>>();
    private readonly Guid _serverGameId = Guid.NewGuid();
    private readonly IUnitsLoader _unitsLoader = Substitute.For<IUnitsLoader>();
    private readonly IMechFactory _mechFactory = Substitute.For<IMechFactory>();
    private readonly IFileCachingService _cachingService = Substitute.For<IFileCachingService>();
    private readonly IRulesProvider _rulesProvider = new TotalWarfareRulesProvider();
    private readonly IToHitCalculator _toHitCalculator = Substitute.For<IToHitCalculator>();
    private readonly IPilotingSkillCalculator _pilotingSkillCalculator = Substitute.For<IPilotingSkillCalculator>();
    private readonly IConsciousnessCalculator _consciousnessCalculator = Substitute.For<IConsciousnessCalculator>();
    private readonly IHeatEffectsCalculator _heatEffectsCalculator = Substitute.For<IHeatEffectsCalculator>();
    private readonly IDispatcherService _dispatcherService = Substitute.For<IDispatcherService>();
    private readonly IGameFactory _gameFactory = Substitute.For<IGameFactory>();
    private readonly IBattleMapFactory _mapFactory = Substitute.For<IBattleMapFactory>();
    private readonly IMapPreviewRenderer _mapPreviewRenderer = Substitute.For<IMapPreviewRenderer>();
    private readonly IMapResourceProvider _mapResourceProvider = Substitute.For<IMapResourceProvider>();
    private readonly IFileService _fileService = Substitute.For<IFileService>();
    private readonly IClipboardService _clipboardService = Substitute.For<IClipboardService>();
    private readonly IRelayHubConfigurationProvider _hubConfigurationProvider = Substitute.For<IRelayHubConfigurationProvider>();
    private readonly IRelayRoomClient _relayRoomClient = Substitute.For<IRelayRoomClient>();
    private readonly IHashService _hashService = Substitute.For<IHashService>();
    private readonly IBotManager _botManager = Substitute.For<IBotManager>();
    private readonly ILogger<StartNewGameViewModel> _vmLogger = Substitute.For<ILogger<StartNewGameViewModel>>();
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private static readonly IBattleMapFactory BattleMapFactory = new BattleMapFactory();

    public StartNewGameViewModelTests()
    {
        _navigationService = Substitute.For<INavigationService>();
        var imageService = Substitute.For<IImageService>();
        _battleMapViewModel =
            new BattleMapViewModel(imageService,
                Substitute.For<ITerrainAssetService>(),
                _localizationService,
                Substitute.For<IDispatcherService>(),
                _rulesProvider,
                Substitute.For<IPlatformService>());
        _navigationService.GetNewViewModelAsync<BattleMapViewModel>().Returns(_battleMapViewModel);
        _unitsLoader.LoadUnits().Returns([MechFactoryTests.CreateDummyMechData()]);



        _clientGame = new ClientGame(_rulesProvider,
            _mechFactory,
            _commandPublisher,
            _toHitCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _mapFactory,
            _hashService,
            _logger);
        _gameFactory.CreateClientGame(_commandPublisher, Arg.Any<Guid?>()).Returns(_clientGame);

        // Set up server game ID
        _gameManager.ServerGameId.Returns(_serverGameId);
        _gameManager.InitializeLocalLobby(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        // Desktop-like platform capable of LAN hosting, so the default host mode is LAN
        _gameManager.CanStartLanServer.Returns(true);

        var map = BattleMapFactory.GenerateMap(5, 5,
            new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        _mapFactory.GenerateMap(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<ITerrainGenerator>()).Returns(map);
        _mapFactory.CreateFromData(Arg.Any<BattleMapData>()).Returns(map);

        _dispatcherService.RunOnUIThread(Arg.InvokeDelegate<Func<Task>>());

        _cachingService.TryGetCachedFile(Arg.Any<string>()).Returns(Task.FromResult<byte[]?>(null));

        _sut = new StartNewGameViewModel(
            _gameManager,
            _unitsLoader,
            _commandPublisher,
            _dispatcherService,
            _gameFactory,
            _mapFactory,
            _cachingService,
            _mapPreviewRenderer,
            _mapResourceProvider,
            _fileService,
            _botManager,
            _vmLogger,
            _localizationService,
            _mechFactory,
            _clipboardService,
            _hubConfigurationProvider,
            _relayRoomClient);
        _sut.AttachHandlers();
        _sut.SetNavigationService(_navigationService);
    }

    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        _sut.MapConfig.ShouldNotBeNull();
        _sut.ServerIpAddress.ShouldBe("LAN Disabled...");
        _sut.CanPublishCommands.ShouldBeTrue();
    }

    [Fact]
    public async Task StartGameCommand_NavigatesToBattleMap()
    {
        await _sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        _sut.MapConfig.SelectedTabIndex = 1; // Switch to the Generate tab
        await ((IAsyncCommand)_sut.StartGameCommand).ExecuteAsync();

        await _navigationService.Received(1).NavigateToViewModelAsync(_battleMapViewModel);
        _battleMapViewModel.Game.ShouldBe(_clientGame);
    }
    
    [Fact]
    public async Task StartGameCommand_ShouldThrow_WhenNavigationServiceDoesNotReturnBattleMapViewModel()
    {
        // Arrange
        _navigationService.GetNewViewModelAsync<BattleMapViewModel>().Returns((BattleMapViewModel?)null);
        _sut.MapConfig.SelectedTabIndex = 1; // Switch to the Generate tab so the Map is non-null
        // Act & Assert
        (await Should.ThrowAsync<Exception>(async () => await ((IAsyncCommand)_sut.StartGameCommand)
            .ExecuteAsync())).Message.ShouldContain("BattleMapViewModel is not registered");
    }

    [Fact]
    public async Task StartGameCommand_ShouldSetBattleMap()
    {
        await _sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        _sut.MapConfig.SelectedTabIndex = 1; // Switch to the Generate tab

        await ((AsyncCommand)_sut.StartGameCommand).ExecuteAsync();

        await _navigationService.Received(1).NavigateToViewModelAsync(_battleMapViewModel);
        _gameManager.Received(1).SetBattleMap(Arg.Any<BattleMap>());
        _gameManager.Received(1).TryStartGame();
    }

    [Fact]
    public async Task StartGameCommand_WhenOnlineMode_LocksOnlineRoomBeforeSettingBattleMap()
    {
        var invokedTcs = new TaskCompletionSource<bool>();
        var lockTcs = new TaskCompletionSource<bool>();
        _gameManager.LockOnlineRoom(Arg.Any<CancellationToken>()).Returns(lockTcs.Task);
        _gameManager.When(x => x.LockOnlineRoom(Arg.Any<CancellationToken>())).Do(_ => invokedTcs.TrySetResult(true));
        await _sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        _sut.MapConfig.SelectedTabIndex = 1; // Switch to the Generate tab
        _sut.IsOnlineMode = true;

        var commandTask = ((AsyncCommand)_sut.StartGameCommand).ExecuteAsync();

        // Wait until LockOnlineRoom is actually invoked, then assert SetBattleMap has not been called
        await invokedTcs.Task;
        _gameManager.DidNotReceive().SetBattleMap(Arg.Any<BattleMap>());

        // Complete the lock task
        lockTcs.SetResult(true);
        await commandTask;

        await _gameManager.Received(1).LockOnlineRoom(Arg.Any<CancellationToken>());
        _gameManager.Received(1).SetBattleMap(Arg.Any<BattleMap>());
        _gameManager.Received(1).TryStartGame();
    }

    [Fact]
    public async Task StartGameCommand_WhenOnlineLockFails_DoesNotSetBattleMapOrNavigate()
    {
        var lockTcs = new TaskCompletionSource<bool>();
        _gameManager.LockOnlineRoom(Arg.Any<CancellationToken>()).Returns(lockTcs.Task);
        _gameManager.OnlineError.Returns((RelayClientError?)null);
        await _sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        _sut.MapConfig.SelectedTabIndex = 1; // Switch to the Generate tab
        _sut.IsOnlineMode = true;

        var commandTask = ((AsyncCommand)_sut.StartGameCommand).ExecuteAsync();

        lockTcs.SetResult(false);
        await commandTask;

        await _gameManager.Received(1).LockOnlineRoom(Arg.Any<CancellationToken>());
        _gameManager.DidNotReceive().SetBattleMap(Arg.Any<BattleMap>());
        _gameManager.DidNotReceive().TryStartGame();
        await _navigationService.DidNotReceive().NavigateToViewModelAsync(Arg.Any<BattleMapViewModel>());
        _sut.HostingError.ShouldNotBeNull();
    }

    [Fact]
    public async Task StartGameCommand_WhenLanMode_DoesNotLockOnlineRoom()
    {
        await _sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        _sut.MapConfig.SelectedTabIndex = 1; // Switch to the Generate tab
        _sut.IsLanMode = true;

        await ((AsyncCommand)_sut.StartGameCommand).ExecuteAsync();

        await _gameManager.DidNotReceive().LockOnlineRoom(Arg.Any<CancellationToken>());
        _gameManager.Received(1).SetBattleMap(Arg.Any<BattleMap>());
        _gameManager.Received(1).TryStartGame();
    }

    [Fact]
    public async Task MapReselection_BroadcastsToServer_AfterDebounceWindowSettles()
    {
        // Act - switch to the Generate tab so the Map changes and the debounced broadcast starts
        _sut.MapConfig.SelectedTabIndex = 1;

        // Assert - the map is not broadcast immediately
        await Task.Delay(200);
        _gameManager.DidNotReceive().SetBattleMap(Arg.Any<BattleMap>());

        // Wait for the 5s debounce window to settle
        await Task.Delay(5400);

        // Assert - the server received the map exactly once
        _gameManager.Received(1).SetBattleMap(Arg.Any<BattleMap>());
    }

    [Fact]
public async Task MapReselection_DuringDebounce_RestartsWindow_AndSendsLatestMap()
    {
        // Arrange - configure two maps returned on successive GenerateMap calls.
        // MapHeight changes trigger debounced regeneration; each produces a new map.
        var firstMap = BattleMapFactory.GenerateMap(3, 3,
            new SingleTerrainGenerator(3, 3, new ClearTerrain()));
        var secondMap = BattleMapFactory.GenerateMap(4, 4,
            new SingleTerrainGenerator(4, 4, new ClearTerrain()));
        _mapFactory.GenerateMap(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<ITerrainGenerator>())
            .Returns(firstMap, secondMap);

        // Capture every map the view model broadcasts so we can assert which one won
        var broadcastMaps = new List<BattleMap>();
        _gameManager.SetBattleMap(Arg.Do<BattleMap>(broadcastMaps.Add));

        // Act - first reselection starts the debounce window
        _sut.MapConfig.SelectedTabIndex = 1;
        _sut.MapConfig.MapHeight = 10;
        // Wait for the MapConfigViewModel's 300ms debounce to settle and emit firstMap
        await Task.Delay(500);

        // Reselect during the 5s window; the pending broadcast must be canceled and restarted
        _sut.MapConfig.MapHeight = 20;
        await Task.Delay(500);

        // Assert - nothing is broadcast while the (restarted) 5s window is still open
        _gameManager.DidNotReceive().SetBattleMap(Arg.Any<BattleMap>());

        // Wait past the first window's expiry and the restarted window
        await Task.Delay(6000);

        // Assert - the latest map was broadcast exactly once, and it is the second map;
        // emissions of firstMap would have left it (or an extra entry) in the list.
        broadcastMaps.Count.ShouldBe(1);
        broadcastMaps[0].ShouldBe(secondMap);
        _gameManager.Received(1).SetBattleMap(Arg.Any<BattleMap>());
    }

    [Fact]
    public async Task DetachHandlers_CancelsPendingDebouncedMapBroadcast()
    {
        // Act - start a debounced broadcast window, then detach before it settles
        _sut.MapConfig.SelectedTabIndex = 1;
        _sut.DetachHandlers();

        // Wait past the 5s debounce window that would have fired had the VM still been attached
        await Task.Delay(5400);

        // Assert - no delayed SetBattleMap call occurs after the ViewModel leaves
        _gameManager.DidNotReceive().SetBattleMap(Arg.Any<BattleMap>());
    }

    [Fact]
    public void AddPlayer_ShouldAddPlayer_WhenLessThanFourPlayers()
    {
        var initialPlayerCount = _sut.Players.Count;

        _sut.AddPlayerCommand!.Execute(null);

        _sut.Players.Count.ShouldBe(initialPlayerCount + 1);
        _sut.Players.Last().Player.ControlType.ShouldBe(PlayerControlType.Human);
        _sut.CanAddPlayer.ShouldBeTrue();
    }

    [Fact]
    public void AddBotCommand_ShouldAddBotPlayer_WhenLessThanFourPlayers()
    {
        var initialPlayerCount = _sut.Players.Count;

        _sut.AddBotCommand!.Execute(null);

        _sut.Players.Count.ShouldBe(initialPlayerCount + 1);
        _sut.Players.Last().Player.ControlType.ShouldBe(PlayerControlType.Bot);
        _sut.CanAddPlayer.ShouldBeTrue();
    }

    [Fact]
    public void AddPlayer_ShouldNotAddPlayer_WhenFourPlayersAlreadyAdded()
    {
        for (var i = 0; i < 4; i++)
        {
            _sut.AddPlayerCommand!.Execute(null);
        }

        var initialPlayerCount = _sut.Players.Count;

        _sut.AddPlayerCommand!.Execute(null);

        _sut.Players.Count.ShouldBe(initialPlayerCount);
        _sut.CanAddPlayer.ShouldBeFalse();
    }

    [Fact]
    public void CanStartGame_ShouldBeFalse_WhenNoPlayers()
    {
        var result = _sut.CanStartGame;

        result.ShouldBeFalse();
    }

    [Fact]
    public void CanStartGame_ShouldBeFalse_WhenPlayersHaveNoUnits()
    {
        _sut.AddPlayerCommand!.Execute(null);

        var result = _sut.CanStartGame;

        result.ShouldBeFalse();
    }

    [Fact]
    public void CanStartGame_ShouldBeFalse_WhenPlayersHaveUnits_ButDidntJoin()
    {
        var units = new List<UnitData> { MechFactoryTests.CreateDummyMechData() };
        _sut.AddPlayerCommand!.Execute(null);
        _sut.Players.First().AddUnit(units.First());

        var result = _sut.CanStartGame;

        result.ShouldBeFalse();
    }

    [Fact]
    public void CanStartGame_ShouldBeFalse_WhenPlayersHaveUnits_AndPlayerHasJoined()
    {
        _sut.AddPlayerCommand!.Execute(null);
        _sut.Players.First().AddUnit(_sut.AvailableUnits.First());
        _sut.Players.First().Player.Status = PlayerStatus.Joined;

        var result = _sut.CanStartGame;

        result.ShouldBeFalse();
    }

    [Fact]
    public void CanStartGame_ShouldBeTrue_WhenPlayersHaveUnits_AndPlayerIsReady()
    {
        _sut.Players.First().AddUnit(_sut.AvailableUnits.First());
        _sut.Players.First().Player.Status = PlayerStatus.Ready;

        var result = _sut.CanStartGame;

        result.ShouldBeTrue();
    }

    [Fact]
    public void CanStartGame_ShouldBeFalse_WhenOnePlayerHasNoUnits()
    {
        _sut.AddPlayerCommand!.Execute(null);
        _sut.AddPlayerCommand!.Execute(null);
        _sut.Players.First().AddUnit(_sut.AvailableUnits.First());

        var result = _sut.CanStartGame;

        result.ShouldBeFalse();
    }

    [Fact]
    public void CanStartLanServer_Getter_ReturnsValueFromGameManager()
    {
        _gameManager.CanStartLanServer.Returns(true);

        _sut.CanStartLanServer.ShouldBeTrue();

        _gameManager.CanStartLanServer.Returns(false);

        _sut.CanStartLanServer.ShouldBeFalse();
    }

    [Fact]
    public void HostMode_DefaultValue_IsLan()
    {
        _sut.IsLanMode.ShouldBeTrue();
        _sut.IsOnlineMode.ShouldBeFalse();
    }

    [Fact]
    public void IsOnlineMode_WhenSetTrue_NotifiesBothModeProperties()
    {
        var changedProps = new List<string?>();
        _sut.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

        _sut.IsOnlineMode = true;

        _sut.IsOnlineMode.ShouldBeTrue();
        _sut.IsLanMode.ShouldBeFalse();
        changedProps.ShouldContain(nameof(StartNewGameViewModel.IsOnlineMode));
        changedProps.ShouldContain(nameof(StartNewGameViewModel.IsLanMode));
    }

    [Fact]
    public void IsLanMode_WhenAlreadyLan_DoesNotNotify()
    {
        var changedProps = new List<string?>();
        _sut.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

        _sut.IsLanMode = true;

        changedProps.ShouldNotContain(nameof(StartNewGameViewModel.IsLanMode));
        changedProps.ShouldNotContain(nameof(StartNewGameViewModel.IsOnlineMode));
    }

    [Fact]
    public async Task SwitchingHostMode_WhenMultiplayerEnabled_IsRejectedWithoutRestartingTransport()
    {
        var gameManager = CreateOnlineGameManager();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var sut = CreateSut(gameManager, commandPublisher);
        sut.IsOnlineMode = true;
        await ((IAsyncCommand)sut.EnableMultiplayerCommand).ExecuteAsync();
        sut.IsMultiplayerEnabled.ShouldBeTrue();
        sut.CanChangeHostMode.ShouldBeFalse();

        sut.IsLanMode = true;

        sut.IsOnlineMode.ShouldBeTrue();
        sut.IsLanMode.ShouldBeFalse();
        sut.RoomCode.ShouldBe("ABCDEF");
        sut.HostingError.ShouldBeNull();
        await gameManager.Received(1).InitializeLobbyOnline(Arg.Any<CancellationToken>());
        await gameManager.DidNotReceive().InitializeLobby(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteEnableMultiplayer_SetsIsMultiplayerEnabledOnlyAfterHostingCompletes()
    {
        var gameManager = Substitute.For<IGameManager>();
        var initTcs = new TaskCompletionSource();
        gameManager.InitializeLobbyOnline(Arg.Any<CancellationToken>()).Returns(initTcs.Task);
        gameManager.RoomCode.Returns("ABCDEF");
        gameManager.OnlineError.Returns((RelayClientError?)null);
        gameManager.ServerGameId.Returns(Guid.NewGuid());
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var sut = CreateSut(gameManager, commandPublisher);
        sut.IsOnlineMode = true;

        var enableTask = ((IAsyncCommand)sut.EnableMultiplayerCommand).ExecuteAsync();

        sut.IsMultiplayerEnabled.ShouldBeFalse();

        initTcs.SetResult();
        await enableTask;

        sut.IsMultiplayerEnabled.ShouldBeTrue();
        sut.EnableMultiplayerCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteEnableMultiplayer_WhenHostingFails_KeepsCommandAvailableForRetry()
    {
        var gameManager = CreateOnlineGameManager(
            roomCode: null,
            error: new RelayClientError(RelayClientErrorCode.NetworkError, "No connection"),
            isRunning: false);
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var sut = CreateSut(gameManager, commandPublisher);
        sut.IsOnlineMode = true;

        await ((IAsyncCommand)sut.EnableMultiplayerCommand).ExecuteAsync();

        sut.IsMultiplayerEnabled.ShouldBeFalse();
        sut.HostingError.ShouldNotBeNull();
        sut.EnableMultiplayerCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task AttachHandlers_SubscribesOnlyAfterLocalLobbyInitializationCompletes()
    {
        var gameManager = Substitute.For<IGameManager>();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher, Arg.Any<Guid?>()).Returns(_clientGame);
        var initTcs = new TaskCompletionSource<bool>();
        gameManager.InitializeLocalLobby(Arg.Any<CancellationToken>()).Returns(initTcs.Task);
        gameManager.ServerGameId.Returns(Guid.NewGuid());

        var sut = new StartNewGameViewModel(
            gameManager, _unitsLoader,
            commandPublisher, _dispatcherService,
            _gameFactory, _mapFactory, _cachingService, _mapPreviewRenderer,
            _mapResourceProvider, _fileService, _botManager,
            _vmLogger, _localizationService, _mechFactory,
            _clipboardService,
            _hubConfigurationProvider,
            _relayRoomClient);

        sut.AttachHandlers();

        commandPublisher.DidNotReceive().Subscribe(Arg.Any<Action<IGameCommand>>());

        initTcs.SetResult(true);
        await WaitFor(() => commandPublisher.ReceivedCalls().Any());

        commandPublisher.Received(1).Subscribe(Arg.Any<Action<IGameCommand>>());
        sut.LocalGame.ShouldNotBeNull();
    }

    [Fact]
    public async Task AttachHandlers_WhenLocalLobbyInitializationFails_DoesNotSubscribeOrCreateLocalGame()
    {
        var gameManager = Substitute.For<IGameManager>();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher, Arg.Any<Guid?>()).Returns(_clientGame);
        var initTcs = new TaskCompletionSource<bool>();
        gameManager.InitializeLocalLobby(Arg.Any<CancellationToken>()).Returns(initTcs.Task);
        gameManager.ServerGameId.Returns(Guid.NewGuid());

        var sut = new StartNewGameViewModel(
            gameManager, _unitsLoader,
            commandPublisher, _dispatcherService,
            _gameFactory, _mapFactory, _cachingService, _mapPreviewRenderer,
            _mapResourceProvider, _fileService, _botManager,
            _vmLogger, _localizationService, _mechFactory,
            _clipboardService,
            _hubConfigurationProvider,
            _relayRoomClient);

        sut.AttachHandlers();

        initTcs.SetResult(false);
        await Task.Delay(100);

        commandPublisher.DidNotReceive().Subscribe(Arg.Any<Action<IGameCommand>>());
        sut.LocalGame.ShouldBeNull();
    }

    [Fact]
    public async Task InitializeLobbyAndSubscribe_WhenLanMode_CallsInitializeLobby()
    {
        var gameManager = Substitute.For<IGameManager>();
        gameManager.ServerGameId.Returns(Guid.NewGuid());
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var sut = CreateSut(gameManager, commandPublisher);

        await sut.InitializeLobbyAndSubscribe(CancellationToken.None);

        await gameManager.Received(1).InitializeLobby(Arg.Any<CancellationToken>());
        await gameManager.DidNotReceive().InitializeLobbyOnline(
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SettingOnlineMode_DoesNotStartHosting_UntilMultiplayerEnabled()
    {
        var gameManager = CreateOnlineGameManager();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher, Arg.Any<Guid?>()).Returns(_clientGame);
        var sut = CreateSut(gameManager, commandPublisher);

        sut.IsOnlineMode = true;

        // Issue #1327: switching host mode must NOT start hosting automatically
        await Task.Delay(50);
        sut.RoomCode.ShouldBeNull();
        sut.HostingError.ShouldBeNull();
        await gameManager.DidNotReceive().InitializeLobbyOnline(
            Arg.Any<CancellationToken>());
        commandPublisher.DidNotReceive().Subscribe(Arg.Any<Action<IGameCommand>>());
        sut.LocalGame.ShouldBeNull();

        // Explicitly enabling multiplayer starts online hosting
        await ((IAsyncCommand)sut.EnableMultiplayerCommand).ExecuteAsync();

        sut.RoomCode.ShouldBe("ABCDEF");
        sut.HostingError.ShouldBeNull();
        await gameManager.Received(1).InitializeLobbyOnline(
            Arg.Any<CancellationToken>());
        commandPublisher.Received(1).Subscribe(Arg.Any<Action<IGameCommand>>());
        sut.LocalGame.ShouldBe(_clientGame);
    }

    [Fact]
    public async Task InitializeOnlineLobbyAndSubscribe_ResolvesActiveHubNameAndProbesStatus()
    {
        // Arrange
        var gameManager = CreateOnlineGameManager();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var activeHub = new HubConfigData("demo", "Demo Hub", "http://demo.local", string.Empty, true);
        StubActiveHub(activeHub);
        _relayRoomClient.Health(Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions>())
            .Returns((RelayClientError?)null);
        var sut = CreateSut(gameManager, commandPublisher);

        // Act
        sut.IsOnlineMode = true;
        await sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        await WaitFor(() => sut.ActiveHub?.Name == "Demo Hub");

        // Assert
        sut.ActiveHub.ShouldNotBeNull();
        sut.ActiveHub!.Name.ShouldBe("Demo Hub");
        sut.ActiveHub.Status.ShouldBe(HubStatus.Online);
        await _relayRoomClient.Received(1).Health(
            Arg.Any<CancellationToken>(),
            Arg.Is<RelayClientOptions>(o => o!.BaseUrl == activeHub.BaseUrl));
    }

    [Fact]
    public async Task InitializeOnlineLobbyAndSubscribe_WhenHubUnreachable_MarksHubStatusOffline()
    {
        // Arrange
        var gameManager = CreateOnlineGameManager();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        StubActiveHub();
        _relayRoomClient.Health(Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions>())
            .Returns(new RelayClientError(RelayClientErrorCode.Timeout, "timed out"));
        var sut = CreateSut(gameManager, commandPublisher);

        // Act
        sut.IsOnlineMode = true;
        await sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        await WaitFor(() => sut.ActiveHub?.Status == HubStatus.Offline);

        // Assert
        sut.ActiveHub.ShouldNotBeNull();
        sut.ActiveHub!.Status.ShouldBe(HubStatus.Offline);
    }

    [Fact]
    public async Task InitializeLobbyAndSubscribe_WhenLanMode_DoesNotProbeHubStatus()
    {
        // Arrange
        var gameManager = Substitute.For<IGameManager>();
        gameManager.ServerGameId.Returns(Guid.NewGuid());
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var sut = CreateSut(gameManager, commandPublisher);

        // Act
        await sut.InitializeLobbyAndSubscribe(CancellationToken.None);

        // Assert
        await _relayRoomClient.DidNotReceive().Health(Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions>());
    }

    [Fact]
    public async Task SwitchingToLan_BeforeHubResolutionCompletes_KeepsActiveHubCleared()
    {
        // Arrange
        var gameManager = Substitute.For<IGameManager>();
        gameManager.InitializeLobbyOnline(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var hubsTcs = new TaskCompletionSource<IReadOnlyList<HubConfigData>>();
        _hubConfigurationProvider.GetHubs().Returns(hubsTcs.Task);
        _hubConfigurationProvider.GetActiveHubId().Returns(Task.FromResult("demo"));
        var sut = CreateSut(gameManager, commandPublisher);

        // Act - start online hosting explicitly, leaving hub resolution in flight
        sut.IsOnlineMode = true;
        var initTask = sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        await WaitFor(() => _hubConfigurationProvider.ReceivedCalls()
            .Any(c => c.GetMethodInfo().Name == nameof(IRelayHubConfigurationProvider.GetHubs)));

        // Switch to LAN before the hub resolution completes
        sut.IsLanMode = true;
        sut.ActiveHub.ShouldBeNull();

        // Complete the stale hub resolution; it must not resurrect ActiveHub
        hubsTcs.SetResult([new HubConfigData("demo", "Demo Hub", "http://demo.local", string.Empty, true)]);
        await Task.Delay(100);
        await initTask;

        // Assert
        sut.IsLanMode.ShouldBeTrue();
        sut.ActiveHub.ShouldBeNull();
    }

    [Fact]
    public async Task SwitchingToLan_AfterGetHubsBeforeActiveHubId_KeepsActiveHubCleared()
    {
        // Arrange
        var gameManager = Substitute.For<IGameManager>();
        gameManager.InitializeLobbyOnline(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        _hubConfigurationProvider.GetHubs()
            .Returns(Task.FromResult<IReadOnlyList<HubConfigData>>([new HubConfigData("demo", "Demo Hub", "http://demo.local", string.Empty, true)]));
        var activeHubIdTcs = new TaskCompletionSource<string>();
        _hubConfigurationProvider.GetActiveHubId().Returns(activeHubIdTcs.Task);
        var sut = CreateSut(gameManager, commandPublisher);

        // Act - start online hosting explicitly; GetHubs resolves, GetActiveHubId stays in flight
        sut.IsOnlineMode = true;
        var initTask = sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        await WaitFor(() => _hubConfigurationProvider.ReceivedCalls()
            .Any(c => c.GetMethodInfo().Name == nameof(IRelayHubConfigurationProvider.GetActiveHubId)));

        // Switch to LAN before the active hub id resolves
        sut.IsLanMode = true;
        sut.ActiveHub.ShouldBeNull();

        // Complete the stale active-hub resolution; it must not resurrect ActiveHub
        activeHubIdTcs.SetResult("demo");
        await Task.Delay(100);
        await initTask;

        // Assert
        sut.IsLanMode.ShouldBeTrue();
        sut.ActiveHub.ShouldBeNull();
    }

    [Fact]
    public async Task InitializeOnlineLobbyAndSubscribe_WhenProbeThrows_SetsHubStatusOffline()
    {
        // Arrange
        var gameManager = CreateOnlineGameManager();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        StubActiveHub();
        _relayRoomClient.Health(Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions>())
            .ThrowsAsync(new HttpRequestException("probe exploded"));
        var sut = CreateSut(gameManager, commandPublisher);

        // Act
        sut.IsOnlineMode = true;
        await sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        await WaitFor(() => sut.ActiveHub?.Status == HubStatus.Offline);

        // Assert
        sut.ActiveHub.ShouldNotBeNull();
        sut.ActiveHub!.Status.ShouldBe(HubStatus.Offline);
    }

    [Fact]
    public async Task InitializeOnlineLobbyAndSubscribe_WhenProbeCancelled_SetsHubStatusUnknown()
    {
        // Arrange
        var gameManager = CreateOnlineGameManager();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        StubActiveHub();
        var healthTcs = new TaskCompletionSource<RelayClientError?>();
        _relayRoomClient.Health(Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions>())
            .Returns(async ci =>
            {
                var token = ci.Arg<CancellationToken>();
                token.Register(() => healthTcs.TrySetException(new OperationCanceledException(token)));
                return await healthTcs.Task;
            });
        var sut = CreateSut(gameManager, commandPublisher);

        // Act - start online hosting explicitly, leaving the health probe in flight
        sut.IsOnlineMode = true;
        var enableTask = ((IAsyncCommand)sut.EnableMultiplayerCommand).ExecuteAsync();
        await WaitFor(() => sut.ActiveHub?.Status == HubStatus.Checking);

        // Detaching cancels the init token, faulting the in-flight probe with cancellation
        sut.DetachHandlers();
        await WaitFor(() => sut.ActiveHub?.Status == HubStatus.Unknown);

        // Assert - a cancelled probe must not be reported as Offline
        sut.ActiveHub.ShouldNotBeNull();
        sut.ActiveHub!.Status.ShouldBe(HubStatus.Unknown);
        await enableTask;
    }

    [Fact]
    public async Task InitializeLobbyAndSubscribe_WhenOnlineInitFails_SetsHostingErrorAndDoesNotCreateLocalGame()
    {
        var error = new RelayClientError(RelayClientErrorCode.HubAtCapacity, "Hub is full");
        var gameManager = CreateOnlineGameManager(roomCode: null, error: error, isRunning: false);
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var sut = CreateSut(gameManager, commandPublisher);
        sut.IsOnlineMode = true;

        await sut.InitializeLobbyAndSubscribe(CancellationToken.None);

        sut.RoomCode.ShouldBeNull();
        sut.HostingError.ShouldBe("Hub is full");
        commandPublisher.DidNotReceive().Subscribe(Arg.Any<Action<IGameCommand>>());
        sut.LocalGame.ShouldBeNull();
    }

    [Fact]
    public async Task InitializeLobbyAndSubscribe_WhenOnlineInitFails_StillResolvesAndProbesActiveHub()
    {
        // Arrange
        var error = new RelayClientError(RelayClientErrorCode.NetworkError, "No connection");
        var gameManager = CreateOnlineGameManager(roomCode: null, error: error, isRunning: false);
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var activeHub = new HubConfigData("demo", "Demo Hub", "http://demo.local", string.Empty, true);
        StubActiveHub(activeHub);
        _relayRoomClient.Health(Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions>())
            .Returns((RelayClientError?)null);
        var sut = CreateSut(gameManager, commandPublisher);

        // Act
        sut.IsOnlineMode = true;
        await sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        await WaitFor(() => sut.ActiveHub?.Name == "Demo Hub");

        // Assert
        sut.RoomCode.ShouldBeNull();
        sut.HostingError.ShouldBe("No connection");
        sut.ActiveHub.ShouldNotBeNull();
        sut.ActiveHub!.Status.ShouldBe(HubStatus.Online);
        await _relayRoomClient.Received(1).Health(
            Arg.Any<CancellationToken>(),
            Arg.Is<RelayClientOptions>(o => o!.BaseUrl == activeHub.BaseUrl));
    }

    [Fact]
    public async Task InitializeLobbyAndSubscribe_WhenOnlineModeCancelled_DoesNotChangeState()
    {
        var gameManager = CreateOnlineGameManager();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher, Arg.Any<Guid?>()).Returns(_clientGame);
        var sut = CreateSut(gameManager, commandPublisher);
        sut.IsOnlineMode = true;
        await sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        sut.RoomCode.ShouldBe("ABCDEF");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await sut.InitializeLobbyAndSubscribe(cts.Token);

        sut.RoomCode.ShouldBe("ABCDEF");
        sut.HostingError.ShouldBeNull();
    }

    [Fact]
    public async Task CancelAndRestartServer_WhenCancelled_LogsDebugMessage()
    {
        var gameManager = Substitute.For<IGameManager>();
        gameManager.InitializeLobby(Arg.Any<CancellationToken>()).ThrowsAsync<OperationCanceledException>();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var sut = CreateSut(gameManager, commandPublisher);

        await sut.CancelAndRestartServer();

        sut.HostingError.ShouldBeNull();
        sut.RoomCode.ShouldBeNull();
        commandPublisher.DidNotReceive().Subscribe(Arg.Any<Action<IGameCommand>>());
        
        // Assert - the cancellation was logged
        _vmLogger.Received(1).Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state!.ToString()!.Contains("Lobby initialization cancelled")),
            Arg.Is<Exception?>(e => e == null),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task CancelAndRestartServer_WhenOnlineSucceeds_SetsRoomCodeAndDoesNotNavigate()
    {
        var gameManager = CreateOnlineGameManager();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var sut = CreateSut(gameManager, commandPublisher);
        sut.IsOnlineMode = true;
        sut.MapConfig.SelectedTabIndex = 1;
        sut.SetNavigationService(_navigationService);

        await sut.CancelAndRestartServer();

        sut.RoomCode.ShouldBe("ABCDEF");
        sut.HostingError.ShouldBeNull();
        await _navigationService.DidNotReceive().NavigateToViewModelAsync(_battleMapViewModel);
    }

    [Fact]
    public async Task CancelAndRestartServer_WhenOnlineFails_SetsHostingErrorAndDoesNotNavigate()
    {
        var error = new RelayClientError(RelayClientErrorCode.NetworkError, "No connection");
        var gameManager = CreateOnlineGameManager(roomCode: null, error: error, isRunning: false);
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var sut = CreateSut(gameManager, commandPublisher);
        sut.IsOnlineMode = true;
        sut.MapConfig.SelectedTabIndex = 1;
        sut.SetNavigationService(_navigationService);

        await sut.CancelAndRestartServer();

        sut.HostingError.ShouldBe("No connection");
        await _navigationService.DidNotReceive().NavigateToViewModelAsync(_battleMapViewModel);
    }

    [Fact]
    public async Task CancelAndRestartServer_WhenInitIsCancelled_SilentlyReturns()
    {
        var gameManager = Substitute.For<IGameManager>();
        gameManager.InitializeLobby(Arg.Any<CancellationToken>()).ThrowsAsync<OperationCanceledException>();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var sut = CreateSut(gameManager, commandPublisher);

        await sut.CancelAndRestartServer();
        await sut.CancelAndRestartServer();

        sut.HostingError.ShouldBeNull();
        sut.RoomCode.ShouldBeNull();
        commandPublisher.DidNotReceive().Subscribe(Arg.Any<Action<IGameCommand>>());
    }

    [Fact]
    public async Task DetachHandlers_WhileLanInitializationIsPending_CancelsInitAndDisablesMultiplayer()
    {
        // Arrange - InitializeLobby stays pending until its token is cancelled
        var gameManager = Substitute.For<IGameManager>();
        gameManager.InitializeLobby(Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var tcs = new TaskCompletionSource();
                ci.Arg<CancellationToken>().Register(() => tcs.SetCanceled());
                return tcs.Task;
            });
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var sut = CreateSut(gameManager, commandPublisher);

        var restartTask = sut.CancelAndRestartServer();
        await Task.Delay(50);

        // Act
        sut.DetachHandlers();

        // Assert - cancellation wins: no error, multiplayer not enabled,
        // and stopping hosting is left to GameManager's cancellation handling.
        await restartTask;
        sut.HostingError.ShouldBeNull();
        sut.IsMultiplayerEnabled.ShouldBeFalse();
        commandPublisher.DidNotReceive().Subscribe(Arg.Any<Action<IGameCommand>>());
        await gameManager.DidNotReceive().StopHosting();
    }

    [Fact]
    public async Task CancelAndRestartServer_WhenCancelledDuringInit_ReturnsWithoutStartingGame()
    {
        var gameManager = Substitute.For<IGameManager>();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var initTcs = new TaskCompletionSource();
        gameManager.InitializeLobby(Arg.Any<CancellationToken>()).Returns(initTcs.Task);
        var sut = CreateSut(gameManager, commandPublisher);
        sut.SetNavigationService(_navigationService);

        var restartTask = sut.CancelAndRestartServer();
        await Task.Delay(50);
        sut.DetachHandlers();
        initTcs.SetResult();

        await restartTask;

        sut.HostingError.ShouldBeNull();
        sut.RoomCode.ShouldBeNull();
        await _navigationService.DidNotReceive().NavigateToViewModelAsync(_battleMapViewModel);
    }

    [Fact]
    public async Task AttachHandlers_ResetsHostModeToOnline_WhenCanStartLanServerIsFalse_AndClearsHostingState()
    {
        var gameManager = Substitute.For<IGameManager>();
        gameManager.InitializeLobbyOnline(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        gameManager.RoomCode.Returns("ABCDEF");
        gameManager.OnlineError.Returns((RelayClientError?)null);
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var sut = CreateSut(gameManager, commandPublisher);
        sut.IsOnlineMode = true;
        await sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        sut.RoomCode.ShouldBe("ABCDEF");

        sut.AttachHandlers();

        sut.IsOnlineMode.ShouldBeTrue();
        sut.IsLanMode.ShouldBeFalse();
        sut.RoomCode.ShouldBeNull();
        sut.HostingError.ShouldBeNull();
    }

    [Fact]
    public async Task AttachHandlers_WhenPlayerJoinedOnlineRoom_KeepsHostingStateAndRoomCode()
    {
        var gameManager = CreateOnlineGameManager();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var sut = CreateSut(gameManager, commandPublisher);
        sut.IsOnlineMode = true;
        await sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        sut.RoomCode.ShouldBe("ABCDEF");

        sut.AddPlayerCommand!.Execute(null);
        sut.Players.Last().Player.Status = PlayerStatus.Joined;

        sut.AttachHandlers();

        sut.IsOnlineMode.ShouldBeTrue();
        sut.IsLanMode.ShouldBeFalse();
        sut.RoomCode.ShouldBe("ABCDEF");
        sut.HostingError.ShouldBeNull();
    }

    [Fact]
    public void HandleServerCommand_JoinGameCommand_AddsRemotePlayer_InvokesNoOpActions()
    {
        var playerId = Guid.NewGuid();
        var joinCommand = new JoinGameCommand
        {
            PlayerId = playerId,
            PlayerName = "RemotePlayer",
            Units = [MechFactoryTests.CreateDummyMechData()],
            Tint = "#00FF00",
            GameOriginId = Guid.NewGuid(),
            PilotAssignments = []
        };

        _sut.HandleServerCommand(joinCommand);
        var remotePlayerVm = _sut.Players.First(p => p.Player.Id == playerId);
        remotePlayerVm.IsLocalPlayer.ShouldBeFalse();

        Should.NotThrow(() =>
        {
            var joinField = typeof(PlayerViewModel).GetField("_joinGameAction",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var readyField = typeof(PlayerViewModel).GetField("_setReadyAction",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var showUnitsField = typeof(PlayerViewModel).GetField("_showAvailableUnits",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            ((Action<PlayerViewModel>)joinField!.GetValue(remotePlayerVm)!).Invoke(remotePlayerVm);
            ((Action<PlayerViewModel>)readyField!.GetValue(remotePlayerVm)!).Invoke(remotePlayerVm);
            ((Func<PlayerViewModel, Task>)showUnitsField!.GetValue(remotePlayerVm)!).Invoke(remotePlayerVm);
        });
    }

    [Fact]
    public void HostingStatusText_WhenNotHostingAndNoState_ReturnsEmpty()
    {
        _sut.HostingStatusText.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task HostingStatusText_WhenHostingErrorSet_ReturnsErrorText()
    {
        var gameManager = CreateOnlineGameManager(
            roomCode: null,
            error: new RelayClientError(RelayClientErrorCode.NetworkError, "No connection"),
            isRunning: false);
        var sut = CreateSut(gameManager, _commandPublisher);
        sut.IsOnlineMode = true;

        await sut.CancelAndRestartServer();

        sut.HostingStatusText.ShouldBe("No connection");
    }

    [Fact]
    public async Task HostingStatusText_WhenRoomReady_ReturnsRoomReadyText()
    {
        _localizationService.GetString("Hosting_RoomReady").Returns("Room ready, join with code: {0}");
        var gameManager = CreateOnlineGameManager();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var sut = CreateSut(gameManager, commandPublisher);
        sut.IsOnlineMode = true;

        await sut.CancelAndRestartServer();

        sut.HostingStatusText.ShouldBe("Room ready, join with code: ABCDEF");
    }

    [Fact]
    public async Task HostingStatusText_WhenHosting_ReturnsStartingText()
    {
        _localizationService.GetString("Hosting_Starting").Returns("Starting hosted game...");
        var gameManager = Substitute.For<IGameManager>();
        var initTcs = new TaskCompletionSource();
        gameManager.InitializeLobbyOnline(Arg.Any<CancellationToken>())
            .Returns(initTcs.Task);
        gameManager.RoomCode.Returns("ABCDEF");
        gameManager.OnlineError.Returns((RelayClientError?)null);
        gameManager.IsOnlineServerRunning.Returns(true);
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var sut = CreateSut(gameManager, commandPublisher);
        sut.IsOnlineMode = true;
        sut.SetNavigationService(_navigationService);

        var restartTask = sut.CancelAndRestartServer();

        sut.HostingStatusText.ShouldBe("Starting hosted game...");

        initTcs.SetResult();
        await restartTask;
    }

    [Fact]
    public void CanChangeHostMode_WhenNoPlayersJoined_IsTrue()
    {
        _sut.CanChangeHostMode.ShouldBeTrue();
    }

    [Fact]
    public void CanChangeHostMode_WhenPlayerJoined_IsFalse()
    {
        _sut.Players.First().Player.Status = PlayerStatus.Joined;

        _sut.CanChangeHostMode.ShouldBeFalse();
    }

    [Fact]
    public async Task SetHostMode_WhenPlayerJoined_DoesNotChangeMode()
    {
        var gameManager = Substitute.For<IGameManager>();
        gameManager.InitializeLobbyOnline(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var sut = CreateSut(gameManager, commandPublisher);
        sut.AddPlayerCommand!.Execute(null);
        sut.Players.First().Player.Status = PlayerStatus.Joined;

        sut.IsOnlineMode = true;

        sut.IsOnlineMode.ShouldBeFalse();
        sut.IsLanMode.ShouldBeTrue();
        sut.CanChangeHostMode.ShouldBeFalse();
        await gameManager.DidNotReceive().InitializeLobbyOnline(
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAndRestartServer_WhenPlayerJoined_DoesNotRestart()
    {
        var gameManager = Substitute.For<IGameManager>();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var sut = CreateSut(gameManager, commandPublisher);
        sut.AddPlayerCommand!.Execute(null);
        sut.Players.First().Player.Status = PlayerStatus.Ready;

        await sut.CancelAndRestartServer();

        await gameManager.DidNotReceive().InitializeLobby(Arg.Any<CancellationToken>());
        await gameManager.DidNotReceive().InitializeLobbyOnline(
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAndRestartServer_WhenPlayerJoinsDuringCancellation_DoesNotRestartLobby()
    {
        var gameManager = Substitute.For<IGameManager>();
        gameManager.ServerGameId.Returns(_serverGameId);
        gameManager.RoomCode.Returns("ABCDEF");
        gameManager.OnlineError.Returns((RelayClientError?)null);
        gameManager.IsOnlineServerRunning.Returns(true);
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var sut = CreateSut(gameManager, commandPublisher);

        var playerId = Guid.NewGuid();
        gameManager.InitializeLobbyOnline(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(0);
                if (ct.CanBeCanceled)
                {
                    // Simulate a remote player joining while the in-flight init is being canceled.
                    ct.Register(() =>
                    {
                        sut.HandleServerCommand(new JoinGameCommand
                        {
                            PlayerId = playerId,
                            PlayerName = "RemotePlayer",
                            Units = [MechFactoryTests.CreateDummyMechData()],
                            Tint = "#00FF00",
                            GameOriginId = _serverGameId,
                            PilotAssignments = []
                        });
                        sut.HandleServerCommand(new UpdatePlayerStatusCommand
                        {
                            PlayerId = playerId,
                            PlayerStatus = PlayerStatus.Joined,
                            GameOriginId = _serverGameId
                        });
                    });
                }
                return Task.CompletedTask;
            });

        sut.IsOnlineMode = true;
        await sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        sut.RoomCode.ShouldBe("ABCDEF");

        await sut.CancelAndRestartServer();

        sut.RoomCode.ShouldBe("ABCDEF");
        sut.HostingError.ShouldBeNull();
        sut.CanChangeHostMode.ShouldBeFalse();
        await gameManager.Received(2).InitializeLobbyOnline(
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleServerCommand_RemotePlayerJoin_DisablesHostModeChange()
    {
        await _sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        var playerId = Guid.NewGuid();
        var joinCommand = new JoinGameCommand
        {
            PlayerId = playerId,
            PlayerName = "RemotePlayer",
            Units = [MechFactoryTests.CreateDummyMechData()],
            Tint = "#00FF00",
            GameOriginId = Guid.NewGuid(),
            PilotAssignments = []
        };

        _sut.HandleServerCommand(joinCommand);
        _sut.HandleServerCommand(new UpdatePlayerStatusCommand
        {
            PlayerId = playerId,
            PlayerStatus = PlayerStatus.Joined,
            GameOriginId = _serverGameId
        });

        _sut.CanChangeHostMode.ShouldBeFalse();
    }

    [Fact]
    public async Task CopyRoomCodeCommand_WhenRoomCodeAvailable_CanExecuteAndRuns()
    {
        var gameManager = CreateOnlineGameManager();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var sut = CreateSut(gameManager, commandPublisher);
        sut.IsOnlineMode = true;

        await sut.InitializeLobbyAndSubscribe(CancellationToken.None);

        sut.CopyRoomCodeCommand.CanExecute(null).ShouldBeTrue();
        Should.NotThrow(() => sut.CopyRoomCodeCommand.Execute(null));
        await _clipboardService.Received(1).SetText("ABCDEF");
    }

    [Fact]
    public async Task CopyRoomCodeCommand_WhenCopySucceeds_SetsRoomCodeCopySucceeded()
    {
        _clipboardService.SetText("ABCDEF").Returns(true);
        var gameManager = CreateOnlineGameManager();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var sut = CreateSut(gameManager, commandPublisher);
        sut.IsOnlineMode = true;

        await sut.InitializeLobbyAndSubscribe(CancellationToken.None);

        Should.NotThrow(() => sut.CopyRoomCodeCommand.Execute(null));

        sut.RoomCodeCopySucceeded.ShouldBe(true);
    }

    [Fact]
    public async Task CopyRoomCodeCommand_WhenCopyFails_SetsRoomCodeCopySucceeded()
    {
        _clipboardService.SetText("ABCDEF").Returns(false);
        var gameManager = CreateOnlineGameManager();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var sut = CreateSut(gameManager, commandPublisher);
        sut.IsOnlineMode = true;

        await sut.InitializeLobbyAndSubscribe(CancellationToken.None);

        Should.NotThrow(() => sut.CopyRoomCodeCommand.Execute(null));

        sut.RoomCodeCopySucceeded.ShouldBe(false);
    }

    [Fact]
    public void RoomCodeCopySucceeded_WhenNoCopyAttempted_IsNullAndStatusTextEmpty()
    {
        _sut.RoomCodeCopySucceeded.ShouldBeNull();
        _sut.CopyRoomCodeStatusText.ShouldBeEmpty();
    }

    [Fact]
    public async Task CopyRoomCodeCommand_WhenCopySucceeds_StatusTextShowsSuccess()
    {
        _localizationService.GetString("Network_CopyRoomCode_Success").Returns("Copied to clipboard");
        _clipboardService.SetText("ABCDEF").Returns(true);
        var gameManager = CreateOnlineGameManager();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var sut = CreateSut(gameManager, commandPublisher);
        sut.IsOnlineMode = true;

        await sut.InitializeLobbyAndSubscribe(CancellationToken.None);

        Should.NotThrow(() => sut.CopyRoomCodeCommand.Execute(null));

        sut.CopyRoomCodeStatusText.ShouldBe("Copied to clipboard");
    }

    [Fact]
    public async Task CopyRoomCodeCommand_WhenCopyFails_StatusTextShowsFailure()
    {
        _localizationService.GetString("Network_CopyRoomCode_Failed").Returns("Copy failed");
        _clipboardService.SetText("ABCDEF").Returns(false);
        var gameManager = CreateOnlineGameManager();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var sut = CreateSut(gameManager, commandPublisher);
        sut.IsOnlineMode = true;

        await sut.InitializeLobbyAndSubscribe(CancellationToken.None);

        Should.NotThrow(() => sut.CopyRoomCodeCommand.Execute(null));

        sut.CopyRoomCodeStatusText.ShouldBe("Copy failed");
    }

    [Fact]
    public async Task SwitchingHostMode_ResetsRoomCodeCopySucceeded()
    {
        _clipboardService.SetText("ABCDEF").Returns(true);
        var gameManager = CreateOnlineGameManager();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        _gameFactory.CreateClientGame(commandPublisher).Returns(_clientGame);
        var sut = CreateSut(gameManager, commandPublisher);
        sut.IsOnlineMode = true;
        await sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        Should.NotThrow(() => sut.CopyRoomCodeCommand.Execute(null));
        sut.RoomCodeCopySucceeded.ShouldBe(true);

        sut.IsLanMode = true;

        sut.RoomCodeCopySucceeded.ShouldBeNull();
        sut.CopyRoomCodeStatusText.ShouldBeEmpty();
    }

    private StartNewGameViewModel CreateSut(
        IGameManager? gameManager = null,
        ICommandPublisher? commandPublisher = null) => new(
        gameManager ?? _gameManager,
        _unitsLoader,
        commandPublisher ?? _commandPublisher,
        _dispatcherService,
        _gameFactory,
        _mapFactory,
        _cachingService,
        _mapPreviewRenderer,
        _mapResourceProvider,
        _fileService,
        _botManager,
        _vmLogger,
        _localizationService,
        _mechFactory,
        _clipboardService,
        _hubConfigurationProvider,
        _relayRoomClient);

    private static IGameManager CreateOnlineGameManager(
        string? roomCode = "ABCDEF",
        RelayClientError? error = null,
        bool isRunning = true)
    {
        var gameManager = Substitute.For<IGameManager>();
        gameManager.InitializeLobbyOnline(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        gameManager.RoomCode.Returns(roomCode);
        gameManager.OnlineError.Returns(error);
        gameManager.IsOnlineServerRunning.Returns(isRunning);
        return gameManager;
    }

    private void StubActiveHub(HubConfigData? activeHub = null)
    {
        activeHub ??= new HubConfigData("demo", "Demo Hub", "http://demo.local", string.Empty, true);
        _hubConfigurationProvider.GetHubs()
            .Returns(Task.FromResult<IReadOnlyList<HubConfigData>>([activeHub]));
        _hubConfigurationProvider.GetActiveHubId().Returns(Task.FromResult(activeHub.Id));
    }

    [Fact]
    public async Task HandleServerCommand_JoinGameCommand_AddsRemotePlayer()
    {
        await _sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        var playerId = Guid.NewGuid();
        const string playerName = "RemotePlayer";
        const string playerTint = "#00FF00";
        var unitId = Guid.NewGuid();
        var units = new List<UnitData> { MechFactoryTests.CreateDummyMechData() with { Id = unitId } };
        
        var joinCommand = new JoinGameCommand
        {
            PlayerId = playerId,
            PlayerName = playerName,
            Units = units,
            Tint = playerTint,
            GameOriginId = Guid.NewGuid(),
            PilotAssignments = []
        };

        _sut.HandleServerCommand(joinCommand);

        var addedPlayerVm = _sut.Players.FirstOrDefault(p => p.Player.Id == playerId);
        addedPlayerVm.ShouldNotBeNull();
        addedPlayerVm.Player.Name.ShouldBe(playerName);
        addedPlayerVm.Player.Tint.ShouldBe(playerTint);
        addedPlayerVm.IsLocalPlayer.ShouldBeFalse();
        addedPlayerVm.Units.Count.ShouldBe(units.Count);
        addedPlayerVm.Units.First().Id.ShouldBe(unitId);
    }

    [Fact]
    public async Task PublishJoinCommand_ForLocalPlayer_CallsJoinGameWithUnitsOnClientGame()
    {
        await _sut.InitializeLobbyAndSubscribe(CancellationToken.None);

        _sut.AddPlayerCommand!.Execute(null);
        var localPlayerVm = _sut.Players.First();
        await localPlayerVm.AddUnit(_sut.AvailableUnits.First());
        localPlayerVm.JoinGameCommand.Execute(null);

        _commandPublisher.Received().PublishCommand(Arg.Any<JoinGameCommand>());
        localPlayerVm.Player.Status = PlayerStatus.Joined;
        _sut.CanStartGame.ShouldBeFalse();
    }

    [Fact]
    public async Task PublishJoinCommand_ForBotPlayer_AddsBot_ToBotManager()
    {
        // Arrange
        await _sut.InitializeLobbyAndSubscribe(CancellationToken.None);

        // Add a bot player using AddBotCommand
        _sut.AddBotCommand!.Execute(null);
        var botPlayerVm = _sut.Players.Last();
        await botPlayerVm.AddUnit(_sut.AvailableUnits.First());

        // Verify the player is a bot
        botPlayerVm.Player.ControlType.ShouldBe(PlayerControlType.Bot);

        // Act
        botPlayerVm.JoinGameCommand.Execute(null);

        // Assert
        _botManager.Received(1).AddBot(botPlayerVm.Player);
    }

    [Fact]
    public async Task PublishJoinCommand_ForHumanPlayer_DoesNotAddBot_ToBotManager()
    {
        // Arrange
        await _sut.InitializeLobbyAndSubscribe(CancellationToken.None);

        // Add a human player using AddPlayerCommand
        _sut.AddPlayerCommand!.Execute(null);
        var humanPlayerVm = _sut.Players.Last();
        await humanPlayerVm.AddUnit(_sut.AvailableUnits.First());

        // Verify the player is human
        humanPlayerVm.Player.ControlType.ShouldBe(PlayerControlType.Human);

        // Act
        humanPlayerVm.JoinGameCommand.Execute(null);

        // Assert
        _botManager.DidNotReceive().AddBot(Arg.Any<IPlayer>());
    }

    [Theory]
    [InlineData("http://192.168.1.100:5000", "192.168.1.100")]
    [InlineData(null, "LAN Disabled...")]
    [InlineData("", "LAN Disabled...")]
    [InlineData("invalid-url", "Invalid Address")]
    public void ServerIpAddress_Getter_ReturnsCorrectValueBasedOnGameManager(string? serverUrl, string expectedDisplay)
    {
        _gameManager.GetLanServerAddress().Returns(serverUrl);

        var result = _sut.ServerIpAddress;

        result.ShouldBe(expectedDisplay);
    }

    [Fact]
    public void Dispose_ShouldNotDisposeGameManager()
    {
        _sut.Dispose();

        _gameManager.DidNotReceive().Dispose();
    }

    [Fact]
    public void Dispose_ShouldUnsubscribeFromCommandPublisher()
    {
        _sut.Dispose();

        _commandPublisher.Received(2).Unsubscribe(Arg.Any<Action<IGameCommand>>());
    }

    [Fact]
    public void DetachHandlers_ShouldUnsubscribeFromCommandPublisher()
    {
        _sut.DetachHandlers();

        _commandPublisher.Received(2).Unsubscribe(Arg.Any<Action<IGameCommand>>());
    }

    [Fact]
    public async Task HandleServerCommand_JoinGameCommand_ShouldUpdateLocalPlayerStatus_WhenReceivedFromServer()
    {
        // Arrange
        await _sut.InitializeLobbyAndSubscribe(CancellationToken.None);

        // Add a local player
        _sut.AddPlayerCommand!.Execute(null);
        var localPlayerVm = _sut.Players.First();
        await localPlayerVm.AddUnit(_sut.AvailableUnits.First());

        // Set player status to JoinRequested
        localPlayerVm.Player.Status = PlayerStatus.Joining;

        // Create a join command that appears to come from the server
        var serverGameId = Guid.NewGuid();
        _gameManager.ServerGameId.Returns(serverGameId);

        var joinCommand = new JoinGameCommand
        {
            PlayerId = localPlayerVm.Player.Id,
            PlayerName = localPlayerVm.Player.Name,
            Units = localPlayerVm.GetUnitsData(),
            Tint = localPlayerVm.Player.Tint,
            GameOriginId = serverGameId, // This makes it look like it came from the server
            PilotAssignments = []
        };

        // Act
        _sut.HandleServerCommand(joinCommand);

        // Assert
        localPlayerVm.Status.ShouldBe(PlayerStatus.Joined);
    }

    [Fact]
    public async Task HandleServerCommand_JoinGameCommand_ShouldNotUpdateLocalPlayerStatus_WhenNotFromServer()
    {
        // Arrange
        await _sut.InitializeLobbyAndSubscribe(CancellationToken.None);

        // Add a local player
        _sut.AddPlayerCommand!.Execute(null);
        var localPlayerVm = _sut.Players.First();
        await localPlayerVm.AddUnit(_sut.AvailableUnits.First());

        // Set player status to JoinRequested
        localPlayerVm.Player.Status = PlayerStatus.Joining;

        // Create a join command that appears to come from a client (not the server)
        var serverGameId = Guid.NewGuid();
        var clientGameId = Guid.NewGuid();
        _gameManager.ServerGameId.Returns(serverGameId);

        var joinCommand = new JoinGameCommand
        {
            PlayerId = localPlayerVm.Player.Id,
            PlayerName = localPlayerVm.Player.Name,
            Units = localPlayerVm.GetUnitsData(),
            Tint = localPlayerVm.Player.Tint,
            GameOriginId = clientGameId, // Different from server ID
            PilotAssignments = []
        };

        // Act
        _sut.HandleServerCommand(joinCommand);

        // Assert
        localPlayerVm.Status.ShouldBe(PlayerStatus.Joining); // Status should not change
    }

    [Fact]
    public async Task ExecuteJoinGame_ShouldSetPlayerStatusToJoinRequested()
    {
        // Arrange
        await _sut.InitializeLobbyAndSubscribe(CancellationToken.None);

        // Add a local player
        _sut.AddPlayerCommand!.Execute(null);
        var localPlayerVm = _sut.Players.First();
        await localPlayerVm.AddUnit(_sut.AvailableUnits.First());

        // Initial status should be NotJoined
        localPlayerVm.Status.ShouldBe(PlayerStatus.NotJoined);

        // Act
        localPlayerVm.JoinGameCommand.Execute(null);

        // Assert
        localPlayerVm.Status.ShouldBe(PlayerStatus.Joining);
    }

    [Fact]
    public async Task ExecuteSetReady_ShouldCallSetPlayerReadyOnClientGame()
    {
        // Arrange
        await _sut.InitializeLobbyAndSubscribe(CancellationToken.None);

        // Add a local player
        _sut.AddPlayerCommand!.Execute(null);
        var localPlayerVm = _sut.Players.First();
        await localPlayerVm.AddUnit(_sut.AvailableUnits.First());

        // Set player status to Joined so they can set ready
        localPlayerVm.Player.Status = PlayerStatus.Joined;
        localPlayerVm.RefreshStatus();
        // Add a player to the client game
        _sut.LocalGame.ShouldNotBeNull();
        _sut.LocalGame?.HandleCommand(new JoinGameCommand
        {
            PlayerId = localPlayerVm.Player.Id,
            PlayerName = localPlayerVm.Player.Name,
            Units = [],
            Tint = localPlayerVm.Player.Tint,
            GameOriginId = Guid.NewGuid(),
            PilotAssignments = []
        });

        // Act
        localPlayerVm.SetReadyCommand.Execute(null);
        
        // Assert - verify the command was published with correct parameters
        _commandPublisher.Received().PublishCommand(Arg.Is<UpdatePlayerStatusCommand>(cmd =>
            cmd.PlayerId == localPlayerVm.Player.Id &&
            cmd.PlayerStatus == PlayerStatus.Ready &&
            cmd.GameOriginId == _clientGame.Id &&
            cmd.IdempotencyKey != null
        ));
    }

    [Fact]
    public async Task
        HandleServerCommand_UpdatePlayerStatusCommand_ShouldUpdateLocalPlayerStatus_WhenReceivedFromServer()
    {
        // Arrange
        await _sut.InitializeLobbyAndSubscribe(CancellationToken.None);

        var localPlayerVm = _sut.Players.First();
        await localPlayerVm.AddUnit(_sut.AvailableUnits.First());

        // Set player status to Joined
        localPlayerVm.Player.Status = PlayerStatus.Joined;
        localPlayerVm.RefreshStatus();

        // Create a status update command that appears to come from the server
        var statusCommand = new UpdatePlayerStatusCommand
        {
            PlayerId = localPlayerVm.Player.Id,
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = _serverGameId // This makes it look like it came from the server
        };

        // Act
        _sut.HandleServerCommand(statusCommand);

        // Assert
        localPlayerVm.Status.ShouldBe(PlayerStatus.Ready);
        _sut.CanStartGame.ShouldBeTrue(); // With one ready player, the game should be able to start
    }

    [Fact]
    public async Task HandleServerCommand_UpdatePlayerStatusCommand_ShouldNotUpdateLocalPlayerStatus_WhenNotFromServer()
    {
        // Arrange
        await _sut.InitializeLobbyAndSubscribe(CancellationToken.None);

        // Add a local player
        _sut.AddPlayerCommand!.Execute(null);
        var localPlayerVm = _sut.Players.First();
        await localPlayerVm.AddUnit(_sut.AvailableUnits.First());

        // Set player status to Joined
        localPlayerVm.Player.Status = PlayerStatus.Joined;
        localPlayerVm.RefreshStatus();

        // Create a status update command that appears to come from a client (not the server)
        var clientGameId = Guid.NewGuid();
        var statusCommand = new UpdatePlayerStatusCommand
        {
            PlayerId = localPlayerVm.Player.Id,
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = clientGameId // Different from server ID
        };

        // Act
        _sut.HandleServerCommand(statusCommand);

        // Assert
        localPlayerVm.Status.ShouldBe(PlayerStatus.Joined); // Status should not change
        _sut.CanStartGame.ShouldBeFalse(); // Game should not be able to start
    }

    [Fact]
    public async Task CanStartGame_ShouldBeTrue_WhenAllPlayersAreReady()
    {
        // Arrange
        await _sut.InitializeLobbyAndSubscribe(CancellationToken.None);

        // two players
        // first player is already added
        var player1 = _sut.Players.First();
        await player1.AddUnit(_sut.AvailableUnits.First());

        // Add a second player
        _sut.AddPlayerCommand!.Execute(null);
        var player2 = _sut.Players.Last();
        await player2.AddUnit(_sut.AvailableUnits.First());

        // Set both players to Ready
        player1.Player.Status = PlayerStatus.Ready;
        player1.RefreshStatus();
        player2.Player.Status = PlayerStatus.Ready;
        player2.RefreshStatus();

        // Assert
        _sut.CanStartGame.ShouldBeTrue();
    }

    [Fact]
    public async Task CanStartGame_ShouldBeFalse_WhenSomePlayersAreNotReady()
    {
        // Arrange
        await _sut.InitializeLobbyAndSubscribe(CancellationToken.None);

        // Add two players
        // Add first player
        _sut.AddPlayerCommand!.Execute(null);
        var player1 = _sut.Players.First();
        await player1.AddUnit(_sut.AvailableUnits.First());

        // Add a second player
        _sut.AddPlayerCommand!.Execute(null);
        var player2 = _sut.Players.Last();
        await player2.AddUnit(_sut.AvailableUnits.First());

        // Set only one player to Ready
        player1.Player.Status = PlayerStatus.Ready;
        player1.RefreshStatus();
        player2.Player.Status = PlayerStatus.Joined; // Not ready
        player2.RefreshStatus();

        // Assert
        _sut.CanStartGame.ShouldBeFalse();
    }

    [Fact]
    public void IsNetworkSectionExpanded_DefaultValue_ShouldBeFalse()
    {
        _sut.IsNetworkSectionExpanded.ShouldBeFalse();
    }

    [Fact]
    public void ToggleNetworkSection_ShouldToggleIsNetworkSectionExpanded()
    {
        _sut.IsNetworkSectionExpanded.ShouldBeFalse();

        _sut.ToggleNetworkSection();

        _sut.IsNetworkSectionExpanded.ShouldBeTrue();

        _sut.ToggleNetworkSection();

        _sut.IsNetworkSectionExpanded.ShouldBeFalse();
    }

    [Fact]
    public void IsNetworkSectionExpanded_ShouldNotifyPropertyChanged()
    {
        var changedProps = new List<string?>();
        _sut.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

        _sut.IsNetworkSectionExpanded = true;

        changedProps.ShouldContain(nameof(StartNewGameViewModel.IsNetworkSectionExpanded));
    }

    [Fact]
    public void AttachHandlers_ShouldAddDefaultPlayer()
    {
        // Act called in the constructor
        // Assert
        _sut.Players.Count.ShouldBe(1);
        _sut.Players.First().Player.Name.ShouldStartWith("Player");
        _sut.Players.First().Player.Tint.ShouldNotBeNullOrEmpty();
    }
    
    [Fact]
    public void AttachHandlers_ShouldAddOnlyOnePlayer_WhenCalledMultipleTimes()
    {
        // Act
        _sut.AttachHandlers(); // Second call

        // Assert
        _sut.Players.Count.ShouldBe(1);
        _sut.Players.First().Player.Name.ShouldStartWith("Player");
        _sut.Players.First().Player.Tint.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void AddDefaultPlayer_ShouldLoadCachedPlayer_WhenAvailable()
    {
        // Arrange
        var defaultPlayerData = PlayerData.CreateDefault() with { Name = "Cached Player" };
        _cachingService.TryGetCachedFile("DefaultPlayer")
            .Returns(JsonSerializer.SerializeToUtf8Bytes(defaultPlayerData));
        var sut = new StartNewGameViewModel(
            _gameManager,
            _unitsLoader,
            _commandPublisher,
            _dispatcherService,
            _gameFactory,
            _mapFactory,
            _cachingService,
            _mapPreviewRenderer,
            _mapResourceProvider,
            _fileService,
            _botManager,
            _vmLogger,
            _localizationService,
            _mechFactory,
            _clipboardService,
            _hubConfigurationProvider,
            _relayRoomClient);
        sut.AttachHandlers();

        // Assert
        sut.Players.Count.ShouldBe(1);
        sut.Players.First().Player.Name.ShouldBe("Cached Player");
    }

    [Fact]
    public void AddDefaultPlayer_ShouldSavePlayerToCache()
    {
        // Assert
        _cachingService.Received().SaveToCache("DefaultPlayer", Arg.Any<byte[]>());
    }
    
    [Fact]
    public void OnDefaultPlayerNameChanged_ShouldSavePlayerToCache()
    {
        // Arrange
        var defaultPlayerData = PlayerData.CreateDefault() with { Name = "Cached Player" };
        _cachingService.TryGetCachedFile("DefaultPlayer")
            .Returns(JsonSerializer.SerializeToUtf8Bytes(defaultPlayerData));
        var sut = new StartNewGameViewModel(
            _gameManager,
            _unitsLoader,
            _commandPublisher,
            _dispatcherService,
            _gameFactory,
            _mapFactory,
            _cachingService,
            _mapPreviewRenderer,
            _mapResourceProvider,
            _fileService,
            _botManager,
            _vmLogger,
            _localizationService,
            _mechFactory,
            _clipboardService,
            _hubConfigurationProvider,
            _relayRoomClient);
        sut.AttachHandlers();

        // Act
        sut.Players.First().SaveName();

        // Assert
        _cachingService.Received(2).SaveToCache("DefaultPlayer", Arg.Any<byte[]>());
    }

    [Fact]
    public async Task AddDefaultPlayer_ShouldPrintLogError_WhenCacheLoadFails()
    {
        // Arrange
        _cachingService.TryGetCachedFile("DefaultPlayer").Throws(new Exception("Cache load failed"));

        // Act
        var sut = new StartNewGameViewModel(
            _gameManager,
            _unitsLoader,
            _commandPublisher,
            _dispatcherService,
            _gameFactory,
            _mapFactory,
            _cachingService,
            _mapPreviewRenderer,
            _mapResourceProvider,
            _fileService,
            _botManager,
            _vmLogger,
            _localizationService,
            _mechFactory,
            _clipboardService,
            _hubConfigurationProvider,
            _relayRoomClient);
        await sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        sut.AttachHandlers();
        
        // Assert
        _logger.Received().LogError(Arg.Any<Exception>(), "Error loading default player from cache");
    }

    [Fact]
    public async Task AddDefaultPlayer_ShouldPrintLogError_WhenCacheSaveFails()
    {
        // Arrange
        _cachingService.SaveToCache("DefaultPlayer", Arg.Any<byte[]>()).Throws(new Exception("Cache save failed"));

        // Act
        var sut = new StartNewGameViewModel(
            _gameManager,
            _unitsLoader,
            _commandPublisher,
            _dispatcherService,
            _gameFactory,
            _mapFactory,
            _cachingService,
            _mapPreviewRenderer,
            _mapResourceProvider,
            _fileService,
            _botManager,
            _vmLogger,
            _localizationService,
            _mechFactory,
            _clipboardService,
            _hubConfigurationProvider,
            _relayRoomClient);
        await sut.InitializeLobbyAndSubscribe(CancellationToken.None);
        sut.AttachHandlers();

        // Assert
        _logger.Received().LogError(Arg.Any<Exception>(), "Error saving default player to cache");
    }

    [Fact]
    public void RemovePlayer_ShouldRemoveNonDefaultPlayer_WhenNotJoined()
    {
        // Arrange
        _sut.AddPlayerCommand!.Execute(null); // Add a second player
        var playerToRemove = _sut.Players.Last();
        var initialCount = _sut.Players.Count;

        // Act
        _sut.RemovePlayerCommand.Execute(playerToRemove);

        // Assert
        _sut.Players.Count.ShouldBe(initialCount - 1);
        _sut.Players.ShouldNotContain(playerToRemove);
    }

    [Fact]
    public void RemovePlayer_ShouldNotRemoveDefaultPlayer()
    {
        // Arrange
        var defaultPlayer = _sut.Players.First(); // The first player is the default player
        var initialCount = _sut.Players.Count;

        // Act
        _sut.RemovePlayerCommand.Execute(defaultPlayer);

        // Assert
        _sut.Players.Count.ShouldBe(initialCount); // Count should not change
        _sut.Players.ShouldContain(defaultPlayer); // Default player should still be there
    }

    [Fact]
    public void RemovePlayer_ShouldNotRemovePlayer_WhenPlayerHasJoined()
    {
        // Arrange
        _sut.AddPlayerCommand!.Execute(null); // Add a second player
        var playerToRemove = _sut.Players.Last();
        playerToRemove.Player.Status = PlayerStatus.Joined;
        playerToRemove.RefreshStatus();
        var initialCount = _sut.Players.Count;

        // Act
        _sut.RemovePlayerCommand.Execute(playerToRemove);

        // Assert
        _sut.Players.Count.ShouldBe(initialCount); // Count should not change
        _sut.Players.ShouldContain(playerToRemove); // Player should still be there
    }

    [Fact]
    public void RemovePlayer_ShouldUpdateCanAddPlayer()
    {
        // Arrange
        // Add players until we reach the limit
        for (var i = 0; i < 3; i++)
        {
            _sut.AddPlayerCommand!.Execute(null);
        }
        _sut.CanAddPlayer.ShouldBeFalse(); // Should be at limit (4 players)

        var playerToRemove = _sut.Players.Last();

        // Act
        _sut.RemovePlayerCommand.Execute(playerToRemove);

        // Assert
        _sut.CanAddPlayer.ShouldBeTrue(); // Should be able to add players again
    }

    [Fact]
    public void RemovePlayer_ShouldUpdateCanStartGame()
    {
        // Arrange
        var changedProps = new List<string?>();
        _sut.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);
        var defaultPlayer = _sut.Players.First();
        defaultPlayer.AddUnit(_sut.AvailableUnits.First());
        defaultPlayer.Player.Status = PlayerStatus.Ready;
        defaultPlayer.RefreshStatus();
        _sut.CanStartGame.ShouldBeTrue();

        _sut.AddPlayerCommand!.Execute(null);
        _sut.CanStartGame.ShouldBeFalse();

        var playerToRemove = _sut.Players.Last();

        // Act
        _sut.RemovePlayerCommand.Execute(playerToRemove);

        // Assert
        _sut.CanStartGame.ShouldBeTrue();
        changedProps.ShouldContain(nameof(StartNewGameViewModel.CanStartGame));
    }

    [Fact]
    public async Task ShowAvailableUnitsTable_ShouldAddUnitToPlayer()
    {
        // Arrange
        var unitData = MechFactoryTests.CreateDummyMechData();
        var navigationService = Substitute.For<INavigationService>();
        _sut.SetNavigationService(navigationService);
        navigationService.ShowViewModelForResultAsync<AvailableUnitsTableViewModel, UnitSelectionResult>(Arg.Any<AvailableUnitsTableViewModel>())
            .Returns(new UnitSelectionResult { SelectedUnit = unitData });
        var localPlayerVm = _sut.Players.First();
        var initialUnitCount = localPlayerVm.Units.Count;

        // Act
        await (localPlayerVm.ShowAvailableUnitsCommand as IAsyncCommand)!.ExecuteAsync();
        var finalUnitCount = localPlayerVm.Units.Count;

        // Assert
        finalUnitCount.ShouldBe(initialUnitCount + 1);
    }
    
    [Fact]
    public async Task ShowAvailableUnitsTable_ShouldNotAddUnit_WhenCancelled()
    {
        // Arrange
        MechFactoryTests.CreateDummyMechData();
        var navigationService = Substitute.For<INavigationService>();
        _sut.SetNavigationService(navigationService);
        navigationService.ShowViewModelForResultAsync<AvailableUnitsTableViewModel, UnitSelectionResult>(Arg.Any<AvailableUnitsTableViewModel>())
            .Returns(new UnitSelectionResult { SelectedUnit = null });
        var localPlayerVm = _sut.Players.First();
        var initialUnitCount = localPlayerVm.Units.Count;

        // Act
        await (localPlayerVm.ShowAvailableUnitsCommand as IAsyncCommand)!.ExecuteAsync();
        var finalUnitCount = localPlayerVm.Units.Count;

        // Assert
        finalUnitCount.ShouldBe(initialUnitCount);
    }

    [Fact]
    public async Task DetachHandlers_CancelsInFlightInitialization()
    {
        var gameManager = Substitute.For<IGameManager>();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var initTcs = new TaskCompletionSource<bool>();
        gameManager.InitializeLocalLobby(Arg.Any<CancellationToken>()).Returns(initTcs.Task);
        gameManager.ServerGameId.Returns(Guid.NewGuid());

        var sut = new StartNewGameViewModel(
            gameManager, _unitsLoader,
            commandPublisher, _dispatcherService,
            _gameFactory, _mapFactory, _cachingService, _mapPreviewRenderer,
            _mapResourceProvider, _fileService, _botManager,
            _vmLogger, _localizationService, _mechFactory,
            _clipboardService,
            _hubConfigurationProvider,
            _relayRoomClient);

        sut.AttachHandlers();
        sut.DetachHandlers();
        initTcs.SetResult(true);

        await Task.Delay(100);

        commandPublisher.DidNotReceive().Subscribe(Arg.Any<Action<IGameCommand>>());
        sut.LocalGame.ShouldBeNull();
    }

    [Fact]
    public async Task Dispose_CancelsInFlightInitialization()
    {
        var gameManager = Substitute.For<IGameManager>();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var initTcs = new TaskCompletionSource<bool>();
        gameManager.InitializeLocalLobby(Arg.Any<CancellationToken>()).Returns(initTcs.Task);
        gameManager.ServerGameId.Returns(Guid.NewGuid());

        var sut = new StartNewGameViewModel(
            gameManager, _unitsLoader,
            commandPublisher, _dispatcherService,
            _gameFactory, _mapFactory, _cachingService, _mapPreviewRenderer,
            _mapResourceProvider, _fileService, _botManager,
            _vmLogger, _localizationService, _mechFactory,
            _clipboardService,
            _hubConfigurationProvider,
            _relayRoomClient);

        sut.AttachHandlers();
        sut.Dispose();
        initTcs.SetResult(true);

        await Task.Delay(100);

        commandPublisher.DidNotReceive().Subscribe(Arg.Any<Action<IGameCommand>>());
    }
    [Fact]
    public async Task MultipleAttachHandlers_OnlyOneActiveInitialization()
    {
        var gameManager = Substitute.For<IGameManager>();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var initTcs = new TaskCompletionSource<bool>();
        gameManager.InitializeLocalLobby(Arg.Any<CancellationToken>()).Returns(initTcs.Task);
        gameManager.ServerGameId.Returns(Guid.NewGuid());

        var sut = new StartNewGameViewModel(
            gameManager, _unitsLoader,
            commandPublisher, _dispatcherService,
            _gameFactory, _mapFactory, _cachingService, _mapPreviewRenderer,
            _mapResourceProvider, _fileService, _botManager,
            _vmLogger, _localizationService, _mechFactory,
            _clipboardService,
            _hubConfigurationProvider,
            _relayRoomClient);

        sut.AttachHandlers();
        sut.AttachHandlers();
        initTcs.SetResult(true);

        await Task.Delay(100);

        commandPublisher.Received(1).Subscribe(Arg.Any<Action<IGameCommand>>());
    }

    [Fact]
    public async Task ShowUnitInfo_ShouldNavigateToUnitInfoView_WhenCalled()
    {
        var unitData = MechFactoryTests.CreateDummyMechData();
        var realMechFactory = new MechFactory(
            new TotalWarfareRulesProvider(),
            new ClassicBattletechComponentProvider(),
            Substitute.For<ILocalizationService>());
        var realUnit = realMechFactory.Create(unitData);
        _mechFactory.Create(Arg.Any<UnitData>()).Returns(realUnit);
        var localPlayerVm = _sut.Players.First();
        await localPlayerVm.AddUnit(unitData);
        var unitId = localPlayerVm.Units.First().Id;

        await ((IAsyncCommand<Guid>)localPlayerVm.ShowUnitInfoCommand).ExecuteAsync(unitId);

        await _navigationService.Received(1).ShowViewModelForResultAsync<UnitInfoViewModel, PilotEditResult?>(Arg.Any<UnitInfoViewModel>());
    }

    [Fact]
    public async Task ShowUnitInfo_ShouldPassPilotData_WhenUnitHasPilot()
    {
        var unitData = MechFactoryTests.CreateDummyMechData();
        var realMechFactory = new MechFactory(
            new TotalWarfareRulesProvider(),
            new ClassicBattletechComponentProvider(),
            Substitute.For<ILocalizationService>());
        var realUnit = realMechFactory.Create(unitData);
        _mechFactory.Create(Arg.Any<UnitData>()).Returns(realUnit);
        var localPlayerVm = _sut.Players.First();
        var pilotData = new PilotData();
        await localPlayerVm.AddUnit(unitData, pilotData);
        var unitId = localPlayerVm.Units.First().Id;

        await ((IAsyncCommand<Guid>)localPlayerVm.ShowUnitInfoCommand).ExecuteAsync(unitId);

        await _navigationService.Received(1).ShowViewModelForResultAsync<UnitInfoViewModel, PilotEditResult?>(
            Arg.Is<UnitInfoViewModel>(vm => vm!.HasPilot));
    }

    private static async Task WaitFor(Func<bool> condition, int timeoutMs = 1000, int intervalMs = 20)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if ((DateTime.UtcNow - start).TotalMilliseconds > timeoutMs)
                throw new TimeoutException("Condition not met within timeout");
            await Task.Delay(intervalMs);
        }
    }
}
