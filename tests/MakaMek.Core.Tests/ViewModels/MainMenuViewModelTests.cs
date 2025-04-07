using NSubstitute;
using Shouldly;
using Sanet.MakaMek.Core.ViewModels;
using Sanet.MVVM.Core.Services;
using AsyncAwaitBestPractices.MVVM;

namespace Sanet.MakaMek.Core.Tests.ViewModels;

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
        _sut.JoinGameCommand.ShouldBeNull(); // Not implemented for now
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
}
