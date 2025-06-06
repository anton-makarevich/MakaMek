using AsyncAwaitBestPractices.MVVM;
using NSubstitute;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MVVM.Core.Services;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class MainMenuViewModelTests
{
    private readonly MainMenuViewModel _sut;
    private readonly INavigationService _navigationService;

    public MainMenuViewModelTests()
    {
        _navigationService = Substitute.For<INavigationService>();
        _sut = new MainMenuViewModel();
        _sut.SetNavigationService(_navigationService); 
    }

    [Fact]
    public void Constructor_InitializesCommands()
    {
        // Assert
        _sut.StartNewGameCommand.ShouldNotBeNull();
        _sut.JoinGameCommand.ShouldNotBeNull(); 
        _sut.Version.ShouldStartWith("v");
    }

    [Fact]
    public async Task StartNewGameCommand_WhenExecuted_NavigatesToStartNewGameViewModel()
    {
        // Arrange
        var command = _sut.StartNewGameCommand as IAsyncCommand;
        command.ShouldNotBeNull();

        // Act
        await command.ExecuteAsync();

        // Assert
        await _navigationService.Received(1).NavigateToViewModelAsync<StartNewGameViewModel>();
    }
    
    [Fact]
    public async Task JoinGameCommand_WhenExecuted_NavigatesToJoinGameViewModel()
    {
        // Arrange
        var command = _sut.JoinGameCommand as IAsyncCommand;
        command.ShouldNotBeNull();

        // Act
        await command.ExecuteAsync();

        // Assert
        await _navigationService.Received(1).NavigateToViewModelAsync<JoinGameViewModel>();
    }
}
