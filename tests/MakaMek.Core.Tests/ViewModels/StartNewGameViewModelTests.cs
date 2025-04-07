using AsyncAwaitBestPractices.MVVM;
using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Core.ViewModels;
using Sanet.MVVM.Core.Services;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Factory;

namespace Sanet.MakaMek.Core.Tests.ViewModels;

public class StartNewGameViewModelTests
{
    private readonly StartNewGameViewModel _sut;
    private readonly INavigationService _navigationService;
    private readonly BattleMapViewModel _battleMapViewModel;
    private readonly IGameManager _gameManager;
    private readonly ICommandPublisher _commandPublisher;
    private readonly ClientGame _clientGame; 
    private readonly Guid _serverGameId = Guid.NewGuid();

    public StartNewGameViewModelTests()
    {
        _navigationService = Substitute.For<INavigationService>();
        var localizationService = Substitute.For<ILocalizationService>();
        var imageService = Substitute.For<IImageService>();
        _battleMapViewModel = new BattleMapViewModel(imageService, localizationService);
        _navigationService.GetViewModel<BattleMapViewModel>().Returns(_battleMapViewModel);
        
        var rulesProvider = new ClassicBattletechRulesProvider(); 
        _gameManager = Substitute.For<IGameManager>();
        _commandPublisher = Substitute.For<ICommandPublisher>(); 
        var toHitCalculator = Substitute.For<IToHitCalculator>(); 
        var dispatcherService = Substitute.For<IDispatcherService>(); 
        var gameFactory = Substitute.For<IGameFactory>(); 

        _clientGame = new ClientGame(rulesProvider, _commandPublisher, toHitCalculator); 
        gameFactory.CreateClientGame(rulesProvider, _commandPublisher, toHitCalculator)
                    .Returns(_clientGame);
        
        // Set up server game ID
        _gameManager.ServerGameId.Returns(_serverGameId);

        dispatcherService.RunOnUIThread(Arg.InvokeDelegate<Action>());

        _sut = new StartNewGameViewModel(
            _gameManager,
            rulesProvider, 
            _commandPublisher, 
            toHitCalculator, 
            dispatcherService, 
            gameFactory); 
        _sut.SetNavigationService(_navigationService);
    }

    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        _sut.MapWidth.ShouldBe(15);
        _sut.MapHeight.ShouldBe(17);
        _sut.ForestCoverage.ShouldBe(20);
        _sut.LightWoodsPercentage.ShouldBe(30);
        _sut.IsLightWoodsEnabled.ShouldBeTrue();
        _sut.ServerIpAddress.ShouldBe("LAN Disabled..."); 
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(50, true)]
    public void ForestCoverage_WhenChanged_UpdatesLightWoodsEnabled(int coverage, bool expectedEnabled)
    {
        _sut.ForestCoverage = coverage;

        _sut.IsLightWoodsEnabled.ShouldBe(expectedEnabled);
    }

    [Fact]
    public async Task StartGameCommand_WithZeroForestCoverage_CreatesClearTerrainMap()
    {
        await _sut.InitializeLobbyAndSubscribe(); 
         
        await ((IAsyncCommand)_sut.StartGameCommand).ExecuteAsync();

        _clientGame.BattleMap.ShouldNotBeNull();
        _battleMapViewModel.Game.ShouldBe(_clientGame); 
    }

    [Fact]
    public async Task StartGameCommand_WithForestCoverage_CreatesForestMap()
    {
        await _sut.InitializeLobbyAndSubscribe(); 
        _sut.ForestCoverage = 100;
        _sut.LightWoodsPercentage = 100;
        await ((IAsyncCommand)_sut.StartGameCommand).ExecuteAsync();

        _clientGame.BattleMap.ShouldNotBeNull();
        _battleMapViewModel.Game.ShouldBe(_clientGame); 
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
    public void MapWidth_SetAndGet_ShouldUpdateCorrectly()
    {
        var newWidth = 20;

        _sut.MapWidth = newWidth;

        _sut.MapWidth.ShouldBe(newWidth);
    }

    [Fact]
    public async Task StartGameCommand_ShouldSetBattleMap()
    {
        await _sut.InitializeLobbyAndSubscribe(); 
 
        await ((AsyncCommand)_sut.StartGameCommand).ExecuteAsync();

        await _navigationService.Received(1).NavigateToViewModelAsync(_battleMapViewModel);
        _gameManager.Received(1).SetBattleMap(Arg.Any<BattleMap>());
        _clientGame.BattleMap.ShouldNotBeNull(); 
    }
    
    [Fact]
    public void AddPlayer_ShouldAddPlayer_WhenLessThanFourPlayers()
    {
        var initialPlayerCount = _sut.Players.Count;

        _sut.AddPlayerCommand.Execute(null);

        _sut.Players.Count.ShouldBe(initialPlayerCount + 1);
        _sut.CanAddPlayer.ShouldBeTrue();
    }

    [Fact]
    public void AddPlayer_ShouldNotAddPlayer_WhenFourPlayersAlreadyAdded()
    {
        for (var i = 0; i < 4; i++)
        {
            _sut.AddPlayerCommand.Execute(null);
        }
        var initialPlayerCount = _sut.Players.Count;

        _sut.AddPlayerCommand.Execute(null);

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
        _sut.AddPlayerCommand.Execute(null); 

        var result = _sut.CanStartGame;

        result.ShouldBeFalse();
    }

    [Fact]
    public void CanStartGame_ShouldBeFalse_WhenPlayersHaveUnits_ButDidntJoin()
    {
        var units = new List<UnitData> { MechFactoryTests.CreateDummyMechData() };
        _sut.InitializeUnits(units);
        _sut.AddPlayerCommand.Execute(null);
        _sut.Players.First().SelectedUnit = units.First();
        _sut.Players.First().AddUnitCommand.Execute(null);
    
        var result = _sut.CanStartGame;
    
        result.ShouldBeFalse();
    }
    
    [Fact]
    public void CanStartGame_ShouldBeFalse_WhenPlayersHaveUnits_AndPlayerHasJoined()
    {
        var units = new List<UnitData> { MechFactoryTests.CreateDummyMechData() };
        _sut.InitializeUnits(units);
        _sut.AddPlayerCommand.Execute(null);
        _sut.Players.First().SelectedUnit = units.First();
        _sut.Players.First().AddUnitCommand.Execute(null);
        _sut.Players.First().Player.Status = PlayerStatus.Joined;
    
        var result = _sut.CanStartGame;
    
        result.ShouldBeFalse();
    }
    
    [Fact]
    public void CanStartGame_ShouldBeTrue_WhenPlayersHaveUnits_AndPlayerIsReady()
    {
        var units = new List<UnitData> { MechFactoryTests.CreateDummyMechData() };
        _sut.InitializeUnits(units);
        _sut.AddPlayerCommand.Execute(null);
        _sut.Players.First().SelectedUnit = units.First();
        _sut.Players.First().AddUnitCommand.Execute(null);
        _sut.Players.First().Player.Status = PlayerStatus.Ready;
    
        var result = _sut.CanStartGame;
    
        result.ShouldBeTrue();
    }
    
    [Fact]
    public void CanStartGame_ShouldBeFalse_WhenOnePlayerHasNoUnits()
    {
        var units = new List<UnitData> { MechFactoryTests.CreateDummyMechData() };
        _sut.InitializeUnits(units);
        _sut.AddPlayerCommand.Execute(null); 
        _sut.AddPlayerCommand.Execute(null); 
        _sut.Players.First().SelectedUnit = units.First();
        _sut.Players.First().AddUnitCommand.Execute(null);
    
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
        var units = new List<UnitData> { MechFactoryTests.CreateDummyMechData() };
        var joinCommand = new JoinGameCommand
        {
            PlayerId = playerId,
            PlayerName = playerName,
            Units = units,
            Tint = playerTint,
            GameOriginId = Guid.NewGuid()
        };

        _sut.HandleServerCommand(joinCommand); 

        var addedPlayerVm = _sut.Players.FirstOrDefault(p => p.Player.Id == playerId);
        addedPlayerVm.ShouldNotBeNull();
        addedPlayerVm.Player.Name.ShouldBe(playerName);
        addedPlayerVm.Player.Tint.ShouldBe(playerTint);
        addedPlayerVm.IsLocalPlayer.ShouldBeFalse();
        addedPlayerVm.Units.Count.ShouldBe(units.Count);
        addedPlayerVm.Units.First().Id.ShouldBe(units.First().Id);
    }
    
    [Fact]
    public async Task PublishJoinCommand_ForLocalPlayer_CallsJoinGameWithUnitsOnClientGame()
    {
        await _sut.InitializeLobbyAndSubscribe(); 
        var units = new List<UnitData> { MechFactoryTests.CreateDummyMechData() };
        _sut.InitializeUnits(units); 
        _sut.AddPlayerCommand.Execute(null); 
        var localPlayerVm = _sut.Players.First();
        localPlayerVm.SelectedUnit = units.First();
        localPlayerVm.AddUnitCommand.Execute(null); 
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
    public void Dispose_ShouldDisposeGameManager()
    {
        _sut.Dispose();

        _gameManager.Received(1).Dispose();
    }
    
    [Fact]
    public async Task HandleServerCommand_JoinGameCommand_ShouldUpdateLocalPlayerStatus_WhenReceivedFromServer()
    {
        // Arrange
        await _sut.InitializeLobbyAndSubscribe();
        
        // Add a local player
        var units = new List<UnitData> { MechFactoryTests.CreateDummyMechData() };
        _sut.InitializeUnits(units);
        _sut.AddPlayerCommand.Execute(null);
        var localPlayerVm = _sut.Players.First();
        localPlayerVm.SelectedUnit = units.First();
        localPlayerVm.AddUnitCommand.Execute(null);
        
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
            GameOriginId = serverGameId // This makes it look like it came from the server
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
        var units = new List<UnitData> { MechFactoryTests.CreateDummyMechData() };
        _sut.InitializeUnits(units);
        _sut.AddPlayerCommand.Execute(null);
        var localPlayerVm = _sut.Players.First();
        localPlayerVm.SelectedUnit = units.First();
        localPlayerVm.AddUnitCommand.Execute(null);
        
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
            GameOriginId = clientGameId // Different from server ID
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
        var units = new List<UnitData> { MechFactoryTests.CreateDummyMechData() };
        _sut.InitializeUnits(units);
        _sut.AddPlayerCommand.Execute(null);
        var localPlayerVm = _sut.Players.First();
        localPlayerVm.SelectedUnit = units.First();
        localPlayerVm.AddUnitCommand.Execute(null);
        
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
        var units = new List<UnitData> { MechFactoryTests.CreateDummyMechData() };
        _sut.InitializeUnits(units);
        _sut.AddPlayerCommand.Execute(null);
        var localPlayerVm = _sut.Players.First();
        localPlayerVm.SelectedUnit = units.First();
        localPlayerVm.AddUnitCommand.Execute(null);
        
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
            GameOriginId = Guid.NewGuid()
        });
        
        // Act
        localPlayerVm.SetReadyCommand.Execute(null);
        
        // Assert - verify the command was published with correct parameters
        _commandPublisher.Received().PublishCommand(Arg.Is<UpdatePlayerStatusCommand>(cmd => 
            cmd.PlayerId == localPlayerVm.Player.Id && 
            cmd.PlayerStatus == PlayerStatus.Ready && 
            cmd.GameOriginId == _clientGame.Id));
    }
    
    [Fact]
    public async Task HandleServerCommand_UpdatePlayerStatusCommand_ShouldUpdateLocalPlayerStatus_WhenReceivedFromServer()
    {
        // Arrange
        await _sut.InitializeLobbyAndSubscribe();
        
        // Add a local player
        var units = new List<UnitData> { MechFactoryTests.CreateDummyMechData() };
        _sut.InitializeUnits(units);
        _sut.AddPlayerCommand.Execute(null);
        var localPlayerVm = _sut.Players.First();
        localPlayerVm.SelectedUnit = units.First();
        localPlayerVm.AddUnitCommand.Execute(null);
        
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
        _sut.CanStartGame.ShouldBeTrue(); // With one ready player, game should be startable
    }
    
    [Fact]
    public async Task HandleServerCommand_UpdatePlayerStatusCommand_ShouldNotUpdateLocalPlayerStatus_WhenNotFromServer()
    {
        // Arrange
        await _sut.InitializeLobbyAndSubscribe();
        
        // Add a local player
        var units = new List<UnitData> { MechFactoryTests.CreateDummyMechData() };
        _sut.InitializeUnits(units);
        _sut.AddPlayerCommand.Execute(null);
        var localPlayerVm = _sut.Players.First();
        localPlayerVm.SelectedUnit = units.First();
        localPlayerVm.AddUnitCommand.Execute(null);
        
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
        _sut.CanStartGame.ShouldBeFalse(); // Game should not be startable
    }
    
    [Fact]
    public async Task CanStartGame_ShouldBeTrue_WhenAllPlayersAreReady()
    {
        // Arrange
        await _sut.InitializeLobbyAndSubscribe();
        
        // Add two players
        var units = new List<UnitData> { MechFactoryTests.CreateDummyMechData() };
        _sut.InitializeUnits(units);
        
        // Add first player
        _sut.AddPlayerCommand.Execute(null);
        var player1 = _sut.Players.First();
        player1.SelectedUnit = units.First();
        player1.AddUnitCommand.Execute(null);
        
        // Add second player
        _sut.AddPlayerCommand.Execute(null);
        var player2 = _sut.Players.Last();
        player2.SelectedUnit = units.First();
        player2.AddUnitCommand.Execute(null);
        
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
        var units = new List<UnitData> { MechFactoryTests.CreateDummyMechData() };
        _sut.InitializeUnits(units);
        
        // Add first player
        _sut.AddPlayerCommand.Execute(null);
        var player1 = _sut.Players.First();
        player1.SelectedUnit = units.First();
        player1.AddUnitCommand.Execute(null);
        
        // Add second player
        _sut.AddPlayerCommand.Execute(null);
        var player2 = _sut.Players.Last();
        player2.SelectedUnit = units.First();
        player2.AddUnitCommand.Execute(null);
        
        // Set only one player to Ready
        player1.Player.Status = PlayerStatus.Ready;
        player1.RefreshStatus();
        player2.Player.Status = PlayerStatus.Joined; // Not ready
        player2.RefreshStatus();
        
        // Assert
        _sut.CanStartGame.ShouldBeFalse();
    }
}
