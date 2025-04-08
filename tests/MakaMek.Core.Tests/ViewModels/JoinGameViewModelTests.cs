using AsyncAwaitBestPractices.MVVM;
using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Core.ViewModels;
using Sanet.Transport;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.ViewModels;

public class JoinGameViewModelTests
{
    private readonly JoinGameViewModel _viewModel;
    private readonly IRulesProvider _rulesProvider = new ClassicBattletechRulesProvider();
    private readonly IToHitCalculator _toHitCalculator = Substitute.For<IToHitCalculator>();
    private readonly IDispatcherService _dispatcherService = Substitute.For<IDispatcherService>();
    private readonly IGameFactory _gameFactory = Substitute.For<IGameFactory>();
    private readonly ITransportFactory _transportFactory = Substitute.For<ITransportFactory>();
    private readonly CommandTransportAdapter _adapter = Substitute.For<CommandTransportAdapter>();
    private readonly ITransportPublisher _transportPublisher = Substitute.For<ITransportPublisher>();
    private readonly ICommandPublisher _commandPublisher = Substitute.For<ICommandPublisher>();

    public JoinGameViewModelTests()
    {
        var clientGame = new ClientGame(_rulesProvider, _commandPublisher, _toHitCalculator);
        // Configure the adapter to be accessible from the command publisher
        _commandPublisher.Adapter.Returns(_adapter);
        
        // Configure the transport factory to return our mock transport publisher
        _transportFactory.CreateAndStartClientPublisher(Arg.Any<string>())
            .Returns(Task.FromResult(_transportPublisher));
            
        // Configure the game factory to return our mock client game
        _gameFactory.CreateClientGame(_rulesProvider, _commandPublisher, _toHitCalculator)
            .Returns(clientGame);
        
        // Configure dispatcher to execute actions immediately
        _dispatcherService.RunOnUIThread(Arg.Do<Action>(action => action()));
        
        // Create the view model with our mocks
        _viewModel = new JoinGameViewModel(
            _rulesProvider,
            _commandPublisher,
            _toHitCalculator,
            _dispatcherService,
            _gameFactory,
            _transportFactory);
    }

    [Fact]
    public async Task ConnectToServer_ClearsExistingPublishers()
    {
        // Arrange
        _viewModel.ServerAddress = "http://localhost:5000"; // Set a valid server address
        
        // Act
        await ((AsyncCommand)_viewModel.ConnectCommand).ExecuteAsync();
        
        // Assert
        // Verify that ClearPublishers was called on the adapter
        _adapter.Received(1).ClearPublishers();
    }
    
    [Fact]
    public async Task ConnectToServer_AddsNewPublisherAfterClearing()
    {
        // Arrange
        _viewModel.ServerAddress = "http://localhost:5000"; // Set a valid server address
        
        // Act
        await ((AsyncCommand)_viewModel.ConnectCommand).ExecuteAsync();
        
        // Assert
        // Verify that the new publisher was added to the adapter
        _adapter.Received(1).AddPublisher(_transportPublisher);
    }
    
    [Fact]
    public async Task ConnectToServer_SetsIsConnectedToTrue_OnSuccess()
    {
        // Arrange
        _viewModel.ServerAddress = "http://localhost:5000"; // Set a valid server address
        
        // Act
        await ((AsyncCommand)_viewModel.ConnectCommand).ExecuteAsync();
        
        // Assert
        _viewModel.IsConnected.ShouldBeTrue();
        _viewModel.CanPublishCommands.ShouldBeTrue();
    }
    
    [Fact]
    public async Task ConnectToServer_SetsIsConnectedToFalse_OnError()
    {
        // Arrange
        _viewModel.ServerAddress = "http://localhost:5000"; // Set a valid server address
        
        // Configure the factory to throw an exception
        _transportFactory.CreateAndStartClientPublisher(Arg.Any<string>())
            .Returns<Task<ITransportPublisher>>(_ => throw new Exception("Connection failed"));
        
        // Act
        await ((AsyncCommand)_viewModel.ConnectCommand).ExecuteAsync();
        
        // Assert
        _viewModel.IsConnected.ShouldBeFalse();
    }
    
    [Fact]
    public void InitializeClientAsync_CreatesClientGame_WhenLocalGameIsNull()
    {
        // Act
        _viewModel.InitializeClient();
        
        // Assert
        _gameFactory.Received(1).CreateClientGame(_rulesProvider, _commandPublisher, _toHitCalculator);
    }
    
    [Fact]
    public void InitializeClientAsync_DoesNotCreateClientGame_WhenLocalGameExists()
    {
        // Arrange - call once to create the game
        _viewModel.InitializeClient();
        _gameFactory.ClearReceivedCalls();
        
        // Act - call again
        _viewModel.InitializeClient();
        
        // Assert - should not create a new game
        _gameFactory.DidNotReceive().CreateClientGame(Arg.Any<IRulesProvider>(), Arg.Any<ICommandPublisher>(), Arg.Any<IToHitCalculator>());
    }
    
    [Fact]
    public void CanAddPlayer_ReturnsFalse_WhenNotConnected()
    {
        // Arrange
        _viewModel.IsConnected.ShouldBeFalse(); // Default state
        
        // Assert
        _viewModel.CanAddPlayer.ShouldBeFalse();
    }
    
    [Fact]
    public async Task CanAddPlayer_ReturnsTrue_WhenConnectedAndLessThanFourPlayers()
    {
        // Arrange
        _viewModel.ServerAddress = "http://localhost:5000";
        await ((AsyncCommand)_viewModel.ConnectCommand).ExecuteAsync();
        
        // Assert
        _viewModel.CanAddPlayer.ShouldBeTrue();
    }
    
    [Fact]
    public async Task CanAddPlayer_ReturnsFalse_WhenConnectedButFourPlayersExist()
    {
        // Arrange
        _viewModel.ServerAddress = "http://localhost:5000";
        await ((AsyncCommand)_viewModel.ConnectCommand).ExecuteAsync();
        
        // Add 4 players
        for (int i = 0; i < 4; i++)
        {
            await ((AsyncCommand)_viewModel.AddPlayerCommand).ExecuteAsync();
        }
        
        // Assert
        _viewModel.CanAddPlayer.ShouldBeFalse();
    }
    
    [Fact]
    public void CanConnect_ReturnsTrue_WhenServerAddressIsSetAndNotConnected()
    {
        // Arrange
        _viewModel.ServerAddress = "http://localhost:5000";
        
        // Assert
        _viewModel.CanConnect.ShouldBeTrue();
    }
    
    [Fact]
    public void CanConnect_ReturnsFalse_WhenServerAddressIsEmpty()
    {
        // Arrange
        _viewModel.ServerAddress = "";
        
        // Assert
        _viewModel.CanConnect.ShouldBeFalse();
    }
    
    [Fact]
    public async Task CanConnect_ReturnsFalse_WhenAlreadyConnected()
    {
        // Arrange
        _viewModel.ServerAddress = "http://localhost:5000";
        await ((AsyncCommand)_viewModel.ConnectCommand).ExecuteAsync();
        
        // Assert
        _viewModel.CanConnect.ShouldBeFalse();
    }
    
    [Fact]
    public async Task HandleCommandInternal_UpdatePlayerStatusCommand_UpdatesPlayerStatus()
    {
        // Arrange
        _viewModel.InitializeClient();
        _viewModel.InitializeUnits([MechFactoryTests.CreateDummyMechData()]);
        
        // Connect and add a player
        _viewModel.ServerAddress = "http://localhost:5000";
        await ((AsyncCommand)_viewModel.ConnectCommand).ExecuteAsync();
        await ((AsyncCommand)_viewModel.AddPlayerCommand).ExecuteAsync();
        
        var player = _viewModel.Players.First();
        var playerId = player.Player.Id;
        
        // Create status update command
        var statusCommand = new UpdatePlayerStatusCommand
        {
            PlayerId = playerId,
            PlayerStatus = PlayerStatus.Ready
        };
        
        // Act - call the method through HandleServerCommand which will call HandleCommandInternal
        _viewModel.HandleServerCommand(statusCommand);
        
        // Assert
        player.Player.Status.ShouldBe(PlayerStatus.Ready);
    }
    
    [Fact]
    public async Task HandleCommandInternal_JoinGameCommand_AddsNewRemotePlayer()
    {
        // Arrange
        _viewModel.InitializeClient();
        _viewModel.InitializeUnits([MechFactoryTests.CreateDummyMechData()]);
        
        // Connect
        _viewModel.ServerAddress = "http://localhost:5000";
        await ((AsyncCommand)_viewModel.ConnectCommand).ExecuteAsync();
        
        // Create join command for a new remote player
        var remotePlayerId = Guid.NewGuid();
        var joinCommand = new JoinGameCommand
        {
            PlayerId = remotePlayerId,
            PlayerName = "Remote Player",
            Units = [MechFactoryTests.CreateDummyMechData()],
            Tint = "#FFFFFF"
        };
        
        // Act
        _viewModel.HandleServerCommand(joinCommand);
        
        // Assert
        _viewModel.Players.Count.ShouldBe(1);
        _viewModel.Players.First().Player.Id.ShouldBe(remotePlayerId);
        _viewModel.Players.First().IsLocalPlayer.ShouldBeFalse();
    }
    
    [Fact]
    public async Task HandleCommandInternal_JoinGameCommand_UpdatesExistingLocalPlayer()
    {
        // Arrange
        _viewModel.InitializeClient();
        _viewModel.InitializeUnits([MechFactoryTests.CreateDummyMechData()]);
        
        // Connect and add a player
        _viewModel.ServerAddress = "http://localhost:5000";
        await ((AsyncCommand)_viewModel.ConnectCommand).ExecuteAsync();
        await ((AsyncCommand)_viewModel.AddPlayerCommand).ExecuteAsync();
        
        var player = _viewModel.Players.First();
        var playerId = player.Player.Id;
        
        // Create join command for the existing local player
        var joinCommand = new JoinGameCommand
        {
            PlayerId = playerId,
            PlayerName = player.Player.Name,
            Units = [MechFactoryTests.CreateDummyMechData()],
            Tint = "#FFFFFF"
        };
        
        // Act
        _viewModel.HandleServerCommand(joinCommand);
        
        // Assert
        _viewModel.Players.Count.ShouldBe(1); // No new player added
        _viewModel.Players.First().Player.Status.ShouldBe(PlayerStatus.Joined);
    }
}
