using AsyncAwaitBestPractices.MVVM;
using System.Windows.Input;
using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Factory;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Core.ViewModels;
using Sanet.Transport;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.ViewModels;

public class JoinGameViewModelTests
{
    private readonly JoinGameViewModel _viewModel;
    private readonly IRulesProvider _rulesProvider = Substitute.For<IRulesProvider>();
    private readonly IToHitCalculator _toHitCalculator = Substitute.For<IToHitCalculator>();
    private readonly IDispatcherService _dispatcherService = Substitute.For<IDispatcherService>();
    private readonly IGameFactory _gameFactory = Substitute.For<IGameFactory>();
    private readonly ITransportFactory _transportFactory = Substitute.For<ITransportFactory>();
    private readonly CommandTransportAdapter _adapter = Substitute.For<CommandTransportAdapter>();
    private readonly ITransportPublisher _transportPublisher = Substitute.For<ITransportPublisher>();

    public JoinGameViewModelTests()
    {
        var commandPublisher =
            // Setup command publisher with mock adapter
            Substitute.For<ICommandPublisher>();

        // Configure the adapter to be accessible from the command publisher
        commandPublisher.Adapter.Returns(_adapter);
        
        // Configure the transport factory to return our mock transport publisher
        _transportFactory.CreateAndStartClientPublisher(Arg.Any<string>())
            .Returns(Task.FromResult(_transportPublisher));
        
        // Create the view model with our mocks
        _viewModel = new JoinGameViewModel(
            _rulesProvider,
            commandPublisher,
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
}
