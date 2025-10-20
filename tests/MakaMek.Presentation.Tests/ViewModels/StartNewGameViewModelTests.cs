using System.Text.Json;
using AsyncAwaitBestPractices.MVVM;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Players;
using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Models.Map;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MakaMek.Presentation.ViewModels;
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
    private readonly Guid _serverGameId = Guid.NewGuid();
    private readonly IUnitsLoader _unitsLoader = Substitute.For<IUnitsLoader>();
    private readonly IMechFactory _mechFactory = Substitute.For<IMechFactory>();
    private readonly IFileCachingService _cachingService = Substitute.For<IFileCachingService>();
    private readonly IRulesProvider _rulesProvider = new ClassicBattletechRulesProvider();
    private readonly IToHitCalculator _toHitCalculator = Substitute.For<IToHitCalculator>();
    private readonly IPilotingSkillCalculator _pilotingSkillCalculator = Substitute.For<IPilotingSkillCalculator>();
    private readonly IConsciousnessCalculator _consciousnessCalculator = Substitute.For<IConsciousnessCalculator>();
    private readonly IHeatEffectsCalculator _heatEffectsCalculator = Substitute.For<IHeatEffectsCalculator>();
    private readonly IDispatcherService _dispatcherService = Substitute.For<IDispatcherService>();
    private readonly IGameFactory _gameFactory = Substitute.For<IGameFactory>();
    private readonly IBattleMapFactory _mapFactory = Substitute.For<IBattleMapFactory>();
    private readonly IMapPreviewRenderer _mapPreviewRenderer = Substitute.For<IMapPreviewRenderer>();
    private readonly IHashService _hashService = Substitute.For<IHashService>();

    public StartNewGameViewModelTests()
    {
        _navigationService = Substitute.For<INavigationService>();
        var localizationService = Substitute.For<ILocalizationService>();
        var imageService = Substitute.For<IImageService>();
        _battleMapViewModel =
            new BattleMapViewModel(imageService,
                localizationService,
                Substitute.For<IDispatcherService>(),
                _rulesProvider);
        _navigationService.GetNewViewModel<BattleMapViewModel>().Returns(_battleMapViewModel);
        _unitsLoader.LoadUnits().Returns([MechFactoryTests.CreateDummyMechData()]);



        _clientGame = new ClientGame(_rulesProvider,
            _mechFactory,
            _commandPublisher,
            _toHitCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _mapFactory,
            _hashService);
        _gameFactory.CreateClientGame(_rulesProvider,
                _mechFactory,
                _commandPublisher,
                _toHitCalculator,
                _pilotingSkillCalculator,
                _consciousnessCalculator,
                _heatEffectsCalculator,
                _mapFactory,
                _hashService)
            .Returns(_clientGame);

        // Set up server game ID
        _gameManager.ServerGameId.Returns(_serverGameId);

        var map = BattleMapTests.BattleMapFactory.GenerateMap(5, 5,
            new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        _mapFactory.GenerateMap(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<ITerrainGenerator>()).Returns(map);
        _mapFactory.CreateFromData(Arg.Any<IList<HexData>>()).Returns(map);

        _dispatcherService.RunOnUIThread(Arg.InvokeDelegate<Func<Task>>());

        _cachingService.TryGetCachedFile(Arg.Any<string>()).Returns(Task.FromResult<byte[]?>(null));

        _sut = new StartNewGameViewModel(
            _gameManager,
            _unitsLoader,
            _rulesProvider,
            _mechFactory,
            _commandPublisher,
            _toHitCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _dispatcherService,
            _gameFactory,
            _mapFactory,
            _cachingService,
            _mapPreviewRenderer,
            _hashService);
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
        await _sut.InitializeLobbyAndSubscribe();
        await ((IAsyncCommand)_sut.StartGameCommand).ExecuteAsync();

        await _navigationService.Received(1).NavigateToViewModelAsync(_battleMapViewModel);
        _battleMapViewModel.Game.ShouldBe(_clientGame);
    }
    
    [Fact]
    public async Task StartGameCommand_ShouldThrow_WhenNavigationServiceDoesNotReturnBattleMapViewModel()
    {
        // Arrange
        _navigationService.GetNewViewModel<BattleMapViewModel>().Returns((BattleMapViewModel?)null);
        // Act & Assert
        (await Should.ThrowAsync<Exception>(async () => await ((IAsyncCommand)_sut.StartGameCommand)
            .ExecuteAsync())).Message.ShouldContain("BattleMapViewModel is not registered");
    }

    [Fact]
    public async Task StartGameCommand_ShouldSetBattleMap()
    {
        await _sut.InitializeLobbyAndSubscribe();

        await ((AsyncCommand)_sut.StartGameCommand).ExecuteAsync();

        await _navigationService.Received(1).NavigateToViewModelAsync(_battleMapViewModel);
        _gameManager.Received(1).SetBattleMap(Arg.Any<BattleMap>());
    }

    [Fact]
    public void AddPlayer_ShouldAddPlayer_WhenLessThanFourPlayers()
    {
        var initialPlayerCount = _sut.Players.Count;

        _sut.AddPlayerCommand!.Execute(null);

        _sut.Players.Count.ShouldBe(initialPlayerCount + 1);
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
    public async Task HandleServerCommand_JoinGameCommand_AddsRemotePlayer()
    {
        await _sut.InitializeLobbyAndSubscribe();
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
        await _sut.InitializeLobbyAndSubscribe();

        _sut.AddPlayerCommand!.Execute(null);
        var localPlayerVm = _sut.Players.First();
        await localPlayerVm.AddUnit(_sut.AvailableUnits.First());
        localPlayerVm.JoinGameCommand.Execute(null);

        _commandPublisher.Received().PublishCommand(Arg.Any<JoinGameCommand>());
        localPlayerVm.Player.Status = PlayerStatus.Joined;
        _sut.CanStartGame.ShouldBeFalse();
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
    public async Task HandleServerCommand_JoinGameCommand_ShouldUpdateLocalPlayerStatus_WhenReceivedFromServer()
    {
        // Arrange
        await _sut.InitializeLobbyAndSubscribe();

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
            Units = localPlayerVm.Units.ToList(),
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
        await _sut.InitializeLobbyAndSubscribe();

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
            Units = localPlayerVm.Units.ToList(),
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
        await _sut.InitializeLobbyAndSubscribe();

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
        await _sut.InitializeLobbyAndSubscribe();

        // Add a local player
        _sut.AddPlayerCommand!.Execute(null);
        var localPlayerVm = _sut.Players.First();
        await localPlayerVm.AddUnit(_sut.AvailableUnits.First());

        // Set player status to Joined so they can set ready
        localPlayerVm.Player.Status = PlayerStatus.Joined;
        localPlayerVm.RefreshStatus();
        // Add player to client game
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
        await _sut.InitializeLobbyAndSubscribe();

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
        await _sut.InitializeLobbyAndSubscribe();

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
        await _sut.InitializeLobbyAndSubscribe();

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
        await _sut.InitializeLobbyAndSubscribe();

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
            _rulesProvider,
            _mechFactory,
            _commandPublisher,
            _toHitCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _dispatcherService,
            _gameFactory,
            _mapFactory,
            _cachingService,
            _mapPreviewRenderer,
            _hashService);
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
            _rulesProvider,
            _mechFactory,
            _commandPublisher,
            _toHitCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _dispatcherService,
            _gameFactory,
            _mapFactory,
            _cachingService,
            _mapPreviewRenderer,
            _hashService);
        sut.AttachHandlers();

        // Act
        sut.Players.First().SaveName();

        // Assert
        _cachingService.Received(2).SaveToCache("DefaultPlayer", Arg.Any<byte[]>());
    }

    [Fact]
    public void AddDefaultPlayer_ShouldPrintLogError_WhenCacheLoadFails()
    {
        // Arrange
        _cachingService.TryGetCachedFile("DefaultPlayer").Throws(new Exception("Cache load failed"));
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);
        try
        {
            // Act
            var sut = new StartNewGameViewModel(
                _gameManager,
                _unitsLoader,
                _rulesProvider,
                _mechFactory,
                _commandPublisher,
                _toHitCalculator,
                _pilotingSkillCalculator,
                _consciousnessCalculator,
                _heatEffectsCalculator,
                _dispatcherService,
                _gameFactory,
                _mapFactory,
                _cachingService,
                _mapPreviewRenderer,
                _hashService);
            sut.AttachHandlers();

            // Assert
            var output = stringWriter.ToString();
            output.ShouldContain("Error loading default player from cache: Cache load failed");
        }
        finally
        {
            // Restore original console output
            Console.SetOut(originalOut);
        }
    }
    
    [Fact]
    public void AddDefaultPlayer_ShouldPrintLogError_WhenCacheSaveFails()
    {
        // Arrange
        _cachingService.SaveToCache("DefaultPlayer", Arg.Any<byte[]>()).Throws(new Exception("Cache save failed"));
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);
        try
        {
            // Act
            var sut = new StartNewGameViewModel(
                _gameManager,
                _unitsLoader,
                _rulesProvider,
                _mechFactory,
                _commandPublisher,
                _toHitCalculator,
                _pilotingSkillCalculator,
                _consciousnessCalculator,
                _heatEffectsCalculator,
                _dispatcherService,
                _gameFactory,
                _mapFactory,
                _cachingService,
                _mapPreviewRenderer,
                _hashService);
            sut.AttachHandlers();

            // Assert
            var output = stringWriter.ToString();
            output.ShouldContain("Error saving default player to cache: Cache save failed");
        }
        finally
        {
            // Restore original console output
            Console.SetOut(originalOut);
        }
    }
    
    [Fact]
    public async Task ShowAvailableUnitsCommand_ShouldMakeTableVisible()
    {
        // Arrange
        var playerVm = _sut.Players.First();
        _sut.IsTableVisible.ShouldBeFalse();
        playerVm.ShouldNotBeNull();

        // Act
        await ((AsyncCommand)playerVm.ShowAvailableUnitsCommand).ExecuteAsync();

        // Assert
        _sut.IsTableVisible.ShouldBeTrue();
    }

    [Fact]
    public async Task HideTableCommand_ShouldHideTable()
    {
        // Arrange
        var playerVm = _sut.Players.First();
        await ((AsyncCommand)playerVm.ShowAvailableUnitsCommand).ExecuteAsync();
        _sut.IsTableVisible.ShouldBeTrue();

        // Act
        await ((AsyncCommand)_sut.HideTableCommand).ExecuteAsync();

        // Assert
        _sut.IsTableVisible.ShouldBeFalse();
    }

    [Fact]
    public async Task AddUnitCommand_WhenNoUnitSelected_ShouldNotAddUnit()
    {
        // Arrange
        var playerVm = _sut.Players.First();
        await ((AsyncCommand)playerVm.ShowAvailableUnitsCommand).ExecuteAsync();
        var initialUnitCount = playerVm.Units.Count;

        // Ensure no unit is selected in the table
        _sut.AvailableUnitsTableViewModel!.SelectedUnit = null;

        // Act
        await ((AsyncCommand)_sut.AvailableUnitsTableViewModel.AddUnitCommand).ExecuteAsync();

        // Assert
        playerVm.Units.Count.ShouldBe(initialUnitCount);
        _sut.IsTableVisible.ShouldBeTrue(); // Table should remain visible
    }

    [Fact]
    public async Task AddUnitCommand_WhenUnitSelected_ShouldAddUnitToPlayerAndHideTable()
    {
        // Arrange
        var playerVm = _sut.Players.First();
        await ((AsyncCommand)playerVm.ShowAvailableUnitsCommand).ExecuteAsync();
        var initialUnitCount = playerVm.Units.Count;
        var unitToSelect = _sut.AvailableUnits.First();

        // Select a unit in the table
        _sut.AvailableUnitsTableViewModel!.SelectedUnit = unitToSelect;

        // Act
        await ((AsyncCommand)_sut.AvailableUnitsTableViewModel.AddUnitCommand).ExecuteAsync();

        // Assert
        playerVm.Units.Count.ShouldBe(initialUnitCount + 1);
        playerVm.Units.Last().Model.ShouldBe(unitToSelect.Model);
        _sut.IsTableVisible.ShouldBeFalse(); // Table should be hidden
    }

    [Fact]
    public async Task AddUnitCommand_WhenPlayerCannotAddUnits_ShouldNotAddUnit()
    {
        // Arrange
        var playerVm = _sut.Players.First();
        await playerVm.AddUnit(_sut.AvailableUnits.First());
        playerVm.Player.Status = PlayerStatus.Joined; // Player cannot add units when joined
        playerVm.RefreshStatus();
        var initialUnitCount = playerVm.Units.Count;
        var unitToSelect = _sut.AvailableUnits.Last();

        // Select a unit in the table
        _sut.AvailableUnitsTableViewModel!.SelectedUnit = unitToSelect;

        // Act
        await ((AsyncCommand)_sut.AvailableUnitsTableViewModel.AddUnitCommand).ExecuteAsync();

        // Assert
        playerVm.Units.Count.ShouldBe(initialUnitCount); // No new unit should be added
    }

    [Fact]
    public void AvailableUnitsTableViewModel_ShouldHaveCorrectAvailableUnits()
    {
        // Arrange
        var expectedUnits = _sut.AvailableUnits;

        // Act
        var tableUnits = _sut.AvailableUnitsTableViewModel!.FilteredAvailableUnits.ToList();

        // Assert
        tableUnits.ShouldNotBeNull();
        tableUnits.Count.ShouldBe(expectedUnits.Count);
        // Compare units by their key properties rather than exact equality
        tableUnits.Select(u => u.Chassis).ShouldBeEquivalentTo(expectedUnits.Select(u => u.Chassis));
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
    public async Task RemovePlayer_ShouldHideTable_WhenRemovingActivePlayer()
    {
        // Arrange
        _sut.AddPlayerCommand!.Execute(null); // Add a second player
        var playerVm = _sut.Players.Last();
        await ((AsyncCommand)playerVm.ShowAvailableUnitsCommand).ExecuteAsync();
        _sut.IsTableVisible.ShouldBeTrue();
        var playerToRemove = _sut.Players.Last();

        // Act
        _sut.RemovePlayerCommand.Execute(playerToRemove);

        // Assert
        _sut.IsTableVisible.ShouldBeFalse();
    }

    [Fact]
    public void RemovePlayer_ShouldNotRemoveDefaultPlayer()
    {
        // Arrange
        var defaultPlayer = _sut.Players.First(); // First player is the default player
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
}
