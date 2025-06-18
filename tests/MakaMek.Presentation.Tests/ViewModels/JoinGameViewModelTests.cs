using AsyncAwaitBestPractices.MVVM;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MVVM.Core.Services;
using Sanet.Transport;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class JoinGameViewModelTests
{
    private readonly JoinGameViewModel _sut;
    private readonly IRulesProvider _rulesProvider = new ClassicBattletechRulesProvider();
    private readonly IToHitCalculator _toHitCalculator = Substitute.For<IToHitCalculator>();
    private readonly IPilotingSkillCalculator  _pilotingSkillCalculator = Substitute.For<IPilotingSkillCalculator>();
    private readonly IDispatcherService _dispatcherService = Substitute.For<IDispatcherService>();
    private readonly IGameFactory _gameFactory = Substitute.For<IGameFactory>();
    private readonly ITransportFactory _transportFactory = Substitute.For<ITransportFactory>();
    private readonly CommandTransportAdapter _adapter = Substitute.For<CommandTransportAdapter>();
    private readonly ITransportPublisher _transportPublisher = Substitute.For<ITransportPublisher>();
    private readonly ICommandPublisher _commandPublisher = Substitute.For<ICommandPublisher>();
    private readonly IUnitsLoader _unitsLoader = Substitute.For<IUnitsLoader>();
    private readonly IBattleMapFactory _mapFactory = Substitute.For<IBattleMapFactory>();
    private readonly IMechFactory _mechFactory = Substitute.For<IMechFactory>();

    public JoinGameViewModelTests()
    {
        _unitsLoader.LoadUnits().Returns([MechFactoryTests.CreateDummyMechData()]);
        var clientGame = new ClientGame(
            _rulesProvider,
            _mechFactory,
            _commandPublisher, 
            _toHitCalculator,
            _pilotingSkillCalculator,
            _mapFactory);
        // Configure the adapter to be accessible from the command publisher
        _commandPublisher.Adapter.Returns(_adapter);
        
        // Configure the transport factory to return our mock transport publisher
        _transportFactory.CreateAndStartClientPublisher(Arg.Any<string>())
            .Returns(Task.FromResult(_transportPublisher));
            
        // Configure the game factory to return our mock client game
        _gameFactory.CreateClientGame(_rulesProvider,
                _mechFactory,
                _commandPublisher,
                _toHitCalculator,
                _pilotingSkillCalculator,
                _mapFactory)
            .Returns(clientGame);
        
        // Configure dispatcher to execute actions immediately
        _dispatcherService.RunOnUIThread(Arg.InvokeDelegate<Func<Task>>());
        
        // Create the view model with our mocks
        _sut = new JoinGameViewModel(
            _rulesProvider,
            _mechFactory,
            _unitsLoader,
            _commandPublisher,
            _toHitCalculator,
            _pilotingSkillCalculator,
            _dispatcherService,
            _gameFactory,
            _transportFactory,
            _mapFactory);
        _sut.AttachHandlers();
    }

    [Fact]
    public async Task ConnectToServer_ClearsExistingPublishers()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000"; // Set a valid server address
        
        // Act
        await ((AsyncCommand)_sut.ConnectCommand).ExecuteAsync();
        
        // Assert
        // Verify that ClearPublishers was called on the adapter
        _adapter.Received(1).ClearPublishers();
    }
    
    [Fact]
    public async Task ConnectToServer_RequestsLobbyStatus()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000"; // Set a valid server address
        
        // Act
        await ((AsyncCommand)_sut.ConnectCommand).ExecuteAsync();
        
        // Assert
        _commandPublisher.Received().PublishCommand(Arg.Any<RequestGameLobbyStatusCommand>());
    }
    
    [Fact]
    public async Task ConnectToServer_AddsNewPublisherAfterClearing()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000"; // Set a valid server address
        
        // Act
        await ((AsyncCommand)_sut.ConnectCommand).ExecuteAsync();
        
        // Assert
        // Verify that the new publisher was added to the adapter
        _adapter.Received(1).AddPublisher(_transportPublisher);
    }
    
    [Fact]
    public async Task ConnectToServer_SetsIsConnectedToTrue_OnSuccess()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000"; // Set a valid server address
        
        // Act
        await ((AsyncCommand)_sut.ConnectCommand).ExecuteAsync();
        
        // Assert
        _sut.IsConnected.ShouldBeTrue();
        _sut.CanPublishCommands.ShouldBeTrue();
    }
    
    [Fact]
    public async Task ConnectToServer_SetsIsConnectedToFalse_OnError()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000"; // Set a valid server address
        
        // Configure the factory to throw an exception
        _transportFactory.CreateAndStartClientPublisher(Arg.Any<string>())
            .Returns<Task<ITransportPublisher>>(_ => throw new Exception("Connection failed"));
        
        // Act
        await ((AsyncCommand)_sut.ConnectCommand).ExecuteAsync();
        
        // Assert
        _sut.IsConnected.ShouldBeFalse();
    }
    
    [Fact]
    public void ConnectCommand_CreatesClientGame_WhenLocalGameIsNull()
    {
        // Arrange
        _sut.ServerIp="127.0.0.1";
        // Act
        _sut.ConnectCommand.Execute(null);
        
        // Assert
        _gameFactory.Received(1).CreateClientGame(_rulesProvider,
            _mechFactory,
            _commandPublisher,
            _toHitCalculator,
            _pilotingSkillCalculator,
            _mapFactory);
    }
    
    [Fact]
    public void ConnectCommand_DoesNotCreateClientGame_WhenLocalGameExists()
    {
        // Arrange - call once to create the game
        _sut.ServerIp="127.0.0.1";
        _sut.ConnectCommand.Execute(null);
        _gameFactory.ClearReceivedCalls();
        
        // Act - call again
        _sut.ConnectCommand.Execute(null);
        
        // Assert - should not create a new game
        _gameFactory.DidNotReceive().CreateClientGame(
            Arg.Any<IRulesProvider>(),
            Arg.Any<IMechFactory>(),
            Arg.Any<ICommandPublisher>(), 
            Arg.Any<IToHitCalculator>(),
            Arg.Any<IPilotingSkillCalculator>(),
            Arg.Any<IBattleMapFactory>());
    }
    
    [Fact]
    public void CanAddPlayer_ReturnsFalse_WhenNotConnected()
    {
        // Arrange
        _sut.IsConnected.ShouldBeFalse(); // Default state
        
        // Assert
        _sut.CanAddPlayer.ShouldBeFalse();
    }
    
    [Fact]
    public async Task CanAddPlayer_ReturnsTrue_WhenConnectedAndLessThanFourPlayers()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000";
        await ((AsyncCommand)_sut.ConnectCommand).ExecuteAsync();
        
        // Assert
        _sut.CanAddPlayer.ShouldBeTrue();
    }
    
    [Fact]
    public async Task CanAddPlayer_ReturnsFalse_WhenConnectedButFourPlayersExist()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000";
        await ((AsyncCommand)_sut.ConnectCommand).ExecuteAsync();
        
        // Add 4 players
        for (int i = 0; i < 4; i++)
        {
            await ((AsyncCommand)_sut.AddPlayerCommand).ExecuteAsync();
        }
        
        // Assert
        _sut.CanAddPlayer.ShouldBeFalse();
    }
    
    [Fact]
    public void CanConnect_ReturnsTrue_WhenServerAddressIsSetAndNotConnected()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000";
        
        // Assert
        _sut.CanConnect.ShouldBeTrue();
    }
    
    [Fact]
    public void CanConnect_ReturnsFalse_WhenServerAddressIsEmpty()
    {
        // Arrange
        _sut.ServerIp = "";
        
        // Assert
        _sut.CanConnect.ShouldBeFalse();
    }
    
    [Fact]
    public async Task CanConnect_ReturnsFalse_WhenAlreadyConnected()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000";
        await ((AsyncCommand)_sut.ConnectCommand).ExecuteAsync();
        
        // Assert
        _sut.CanConnect.ShouldBeFalse();
    }
    
    [Fact]
    public async Task HandleCommandInternal_UpdatePlayerStatusCommand_UpdatesPlayerStatus()
    {
        // Connect and add a player
        _sut.ServerIp = "http://localhost:5000";
        await ((AsyncCommand)_sut.ConnectCommand).ExecuteAsync();
        await ((AsyncCommand)_sut.AddPlayerCommand).ExecuteAsync();
        
        var player = _sut.Players.First();
        var playerId = player.Player.Id;
        
        // Create status update command
        var statusCommand = new UpdatePlayerStatusCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            PlayerStatus = PlayerStatus.Ready
        };
        
        // Act - call the method through HandleServerCommand which will call HandleCommandInternal
        _sut.HandleServerCommand(statusCommand);
        
        // Assert
        player.Player.Status.ShouldBe(PlayerStatus.Ready);
    }
    
    [Fact]
    public async Task HandleCommandInternal_JoinGameCommand_AddsNewRemotePlayer()
    {
        // Connect
        _sut.ServerIp = "http://localhost:5000";
        await ((AsyncCommand)_sut.ConnectCommand).ExecuteAsync();
        
        // Create join command for a new remote player
        var remotePlayerId = Guid.NewGuid();
        var joinCommand = new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = remotePlayerId,
            PlayerName = "Remote Player",
            Units = [MechFactoryTests.CreateDummyMechData()],
            Tint = "#FFFFFF"
        };
        
        // Act
        _sut.HandleServerCommand(joinCommand);
        
        // Assert
        _sut.Players.Count.ShouldBe(1);
        _sut.Players.First().Player.Id.ShouldBe(remotePlayerId);
        _sut.Players.First().IsLocalPlayer.ShouldBeFalse();
    }
    
    [Fact]
    public async Task HandleCommandInternal_JoinGameCommand_UpdatesExistingLocalPlayer()
    {
        // Connect and add a player
        _sut.ServerIp = "http://localhost:5000";
        await ((AsyncCommand)_sut.ConnectCommand).ExecuteAsync();
        await ((AsyncCommand)_sut.AddPlayerCommand).ExecuteAsync();
        
        var player = _sut.Players.First();
        var playerId = player.Player.Id;
        
        // Create join command for the existing local player
        var joinCommand = new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            PlayerName = player.Player.Name,
            Units = [MechFactoryTests.CreateDummyMechData()],
            Tint = "#FFFFFF"
        };
        
        // Act
        _sut.HandleServerCommand(joinCommand);
        
        // Assert
        _sut.Players.Count.ShouldBe(1); // No new player added
        _sut.Players.First().Player.Status.ShouldBe(PlayerStatus.Joined);
    }

    [Fact]
    public async Task HandleCommandInternal_SetBattleMapCommand_SetsBattleMapAndNavigates()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000";
        await ((AsyncCommand)_sut.ConnectCommand).ExecuteAsync();
        var navigationService = Substitute.For<INavigationService>();
        var localizationService = Substitute.For<ILocalizationService>();
        var imageService = Substitute.For<IImageService>();
        var battleMapViewModel = new BattleMapViewModel(imageService, localizationService,Substitute.For<IDispatcherService>());
        navigationService.GetViewModel<BattleMapViewModel>()
            .Returns(battleMapViewModel);
 
        _sut.SetNavigationService(navigationService);

        // Act
        _sut.HandleServerCommand(new SetBattleMapCommand
        {
            GameOriginId = Guid.NewGuid(),
            MapData = []
        });

        // Assert
        navigationService.Received(1).GetViewModel<BattleMapViewModel>();
        battleMapViewModel.Game.ShouldNotBeNull();
        await navigationService.Received(1).NavigateToViewModelAsync(battleMapViewModel);
    }

    [Fact]
    public void ServerAddress_ShouldIncludeIP_AndPort()
    {
        const string ip = "127.0.0.1";
        const int port = 2439;
        const string hub = "makamekhub";
        
        _sut.ServerIp = ip;
        
        _sut.ServerAddress.ShouldBe($"http://{ip}:{port}/{hub}");
    }
}
