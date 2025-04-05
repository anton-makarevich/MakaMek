using AsyncAwaitBestPractices.MVVM;
using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Core.ViewModels;
using Sanet.MVVM.Core.Services;
using Sanet.MakaMek.Core.Models.Game.Commands.Client; 

namespace Sanet.MakaMek.Core.Tests.ViewModels;

public class StartNewGameViewModelTests
{
    private readonly StartNewGameViewModel _sut;
    private readonly INavigationService _navigationService;
    private readonly BattleMapViewModel _battleMapViewModel;
    private readonly IGameManager _gameManager;
    private readonly ICommandPublisher _commandPublisher; 

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

        // Make dispatcher run action immediately for testing
        dispatcherService.RunOnUIThread(Arg.InvokeDelegate<Action>());

        _sut = new StartNewGameViewModel(
            _gameManager,
            rulesProvider, // Pass mock
            _commandPublisher, // Pass mock
            toHitCalculator, // Pass mock
            dispatcherService); // Pass mock
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
        _sut.ServerIpAddress.ShouldBe("LAN Disabled..."); // Verify default Server IP Address
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
        // Arrange
        _sut.ForestCoverage = 0;
        await _sut.InitializeLobbyAndSubscribe();
        
        // Act
        await ((IAsyncCommand)_sut.StartGameCommand).ExecuteAsync();

        // Assert
        _battleMapViewModel.Game.ShouldNotBeNull();
        var hex = _battleMapViewModel.Game!.BattleMap!.GetHexes().First();
        hex.GetTerrains().ToList().Count.ShouldBe(1);
        hex.GetTerrains().First().ShouldBeOfType<ClearTerrain>();
    }

    [Fact]
    public async Task StartGameCommand_WithForestCoverage_CreatesForestMap()
    {
        // Arrange
        await _sut.InitializeLobbyAndSubscribe();
        _sut.ForestCoverage = 100;
        _sut.LightWoodsPercentage = 100;
        await ((IAsyncCommand)_sut.StartGameCommand).ExecuteAsync();

        _battleMapViewModel.Game.ShouldNotBeNull();
        var hexes = _battleMapViewModel.Game!.BattleMap!.GetHexes().ToList();
        hexes.ShouldContain(h => h.GetTerrains().Any(t => t is LightWoodsTerrain));
    }

    [Fact]
    public async Task StartGameCommand_NavigatesToBattleMap()
    {
        await _sut.InitializeLobbyAndSubscribe();
        await ((IAsyncCommand)_sut.StartGameCommand).ExecuteAsync();

        await _navigationService.Received(1).NavigateToViewModelAsync(_battleMapViewModel);
    }

    [Fact]
    public void MapWidth_SetAndGet_ShouldUpdateCorrectly()
    {
        // Arrange
        var newWidth = 20;

        // Act
        _sut.MapWidth = newWidth;

        // Assert
        _sut.MapWidth.ShouldBe(newWidth);
    }

    [Fact]
    public async Task StartGameCommand_ShouldSetBattleMap()
    {
        // Arrange
        await _sut.InitializeLobbyAndSubscribe();

        // Act
        await ((AsyncCommand)_sut.StartGameCommand).ExecuteAsync();

        // Assert
        await _navigationService.Received(1).NavigateToViewModelAsync(_battleMapViewModel);
        _gameManager.Received(1).SetBattleMap(Arg.Any<BattleMap>());
    }
    
    [Fact]
    public void AddPlayer_ShouldAddPlayer_WhenLessThanFourPlayers()
    {
        // Arrange
        var initialPlayerCount = _sut.Players.Count;

        // Act
        _sut.AddPlayerCommand.Execute(null);

        // Assert
        _sut.Players.Count.ShouldBe(initialPlayerCount + 1);
        _sut.CanAddPlayer.ShouldBeTrue();
    }

    [Fact]
    public void AddPlayer_ShouldNotAddPlayer_WhenFourPlayersAlreadyAdded()
    {
        // Arrange
        for (var i = 0; i < 4; i++)
        {
            _sut.AddPlayerCommand.Execute(null);
        }
        var initialPlayerCount = _sut.Players.Count;

        // Act
        _sut.AddPlayerCommand.Execute(null);

        // Assert
        _sut.Players.Count.ShouldBe(initialPlayerCount); // Should not increase
        _sut.CanAddPlayer.ShouldBeFalse();
    }
    
    [Fact]
    public void CanStartGame_ShouldBeFalse_WhenNoPlayers()
    {
        // Arrange
        // No players added

        // Act
        var result = _sut.CanStartGame;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void CanStartGame_ShouldBeFalse_WhenPlayersHaveNoUnits()
    {
        // Arrange
        _sut.AddPlayerCommand.Execute(null); // Add a player

        // Act
        var result = _sut.CanStartGame;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void CanStartGame_ShouldBeTrue_WhenPlayersHaveUnits()
    {
        // Arrange
        var units = new List<UnitData> { MechFactoryTests.CreateDummyMechData() };
        _sut.InitializeUnits(units);
        _sut.AddPlayerCommand.Execute(null);
        _sut.Players.First().SelectedUnit = units.First();
        _sut.Players.First().AddUnitCommand.Execute(null);
    
        // Act
        var result = _sut.CanStartGame;
    
        // Assert
        result.ShouldBeTrue();
    }
    
    [Fact]
    public void CanStartGame_ShouldBeFalse_WhenOnePlayerHasNoUnits()
    {
        // Arrange
        var units = new List<UnitData> { MechFactoryTests.CreateDummyMechData() };
        _sut.InitializeUnits(units);
        _sut.AddPlayerCommand.Execute(null); // first player
        _sut.AddPlayerCommand.Execute(null); // second player
        _sut.Players.First().SelectedUnit = units.First();
        _sut.Players.First().AddUnitCommand.Execute(null);
    
        // Act
        var result = _sut.CanStartGame;
    
        // Assert
        result.ShouldBeFalse();
    }
    
    [Fact]
    public void CanStartLanServer_Getter_ReturnsValueFromGameManager()
    {
        // Arrange
        _gameManager.CanStartLanServer.Returns(true);
        
        // Act & Assert
        _sut.CanStartLanServer.ShouldBeTrue();
        
        // Arrange
        _gameManager.CanStartLanServer.Returns(false);
        
        // Act & Assert
        _sut.CanStartLanServer.ShouldBeFalse();
    }
    
    [Fact]
    public async Task HandleServerCommand_JoinGameCommand_AddsRemotePlayer()
    {
        // Arrange
        await _sut.InitializeLobbyAndSubscribe(); // Ensure handler is subscribed and ClientGame exists
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

        // Act
        _sut.HandleServerCommand(joinCommand); // Simulate receiving command

        // Assert
        var addedPlayerVm = _sut.Players.FirstOrDefault(p => p.Player.Id == playerId);
        addedPlayerVm.ShouldNotBeNull();
        addedPlayerVm.Player.Name.ShouldBe(playerName);
        addedPlayerVm.Player.Tint.ShouldBe(playerTint);
        addedPlayerVm.IsLocalPlayer.ShouldBeFalse();
        addedPlayerVm.Units.Count.ShouldBe(units.Count);
        // Assuming UnitData has an ID property for comparison
        addedPlayerVm.Units.First().Id.ShouldBe(units.First().Id);
    }
    
    [Fact]
    public async Task PublishJoinCommand_ForLocalPlayer_CallsJoinGameWithUnitsOnClientGame()
    {
        // Arrange
        await _sut.InitializeLobbyAndSubscribe(); // Initializes _localGame internally
        var units = new List<UnitData> { MechFactoryTests.CreateDummyMechData() };
        _sut.InitializeUnits(units); // Make units available
        _sut.AddPlayerCommand.Execute(null); // Add a local player VM
        var localPlayerVm = _sut.Players.First();
        localPlayerVm.SelectedUnit = units.First();
        localPlayerVm.AddUnitCommand.Execute(null); // Add unit to the local player VM
        localPlayerVm.JoinGameCommand.Execute(null);
        
        // Assert
        _commandPublisher.Received().PublishCommand(Arg.Any<JoinGameCommand>());
        _sut.CanStartGame.ShouldBeTrue(); 
    }
    
    [Theory]
    [InlineData("http://192.168.1.100:5000", "192.168.1.100")]
    [InlineData(null, "LAN Disabled...")]
    [InlineData("", "LAN Disabled...")]
    [InlineData("invalid-url", "Invalid Address")]
    public void ServerIpAddress_Getter_ReturnsCorrectValueBasedOnGameManager(string? serverUrl, string expectedDisplay)
    {
        // Arrange
        _gameManager.GetLanServerAddress().Returns(serverUrl);

        // Act
        var result = _sut.ServerIpAddress;

        // Assert
        result.ShouldBe(expectedDisplay);
    }

    [Fact]
    public void Dispose_ShouldDisposeGameManager()
    {
        // Act
        _sut.Dispose();

        // Assert
        _gameManager.Received(1).Dispose();
    }
}
