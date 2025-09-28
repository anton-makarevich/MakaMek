using AsyncAwaitBestPractices.MVVM;
using NSubstitute;
using Sanet.MakaMek.Core.Services;
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
        var unitCachingService = Substitute.For<IUnitCachingService>();

        // Setup default behavior for unit caching service
        unitCachingService.GetAvailableModels().Returns(["LCT-1V", "SHD-2D"]);

        _sut = new MainMenuViewModel(unitCachingService, 0);
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
    
    [Fact]
    public async Task PreloadUnits_WhenExceptionThrown_SetsErrorTextAndKeepsLoadingTrue()
    {
        // Arrange
        var unitCachingService = Substitute.For<IUnitCachingService>();
        const string errorMessage = "Test error message";
        unitCachingService.When(x => x.GetAvailableModels())
            .Throw(new Exception(errorMessage));
            
        var sut = new MainMenuViewModel(unitCachingService, 0);
        sut.SetNavigationService(_navigationService);
        
        // Small delay to allow the background task to complete
        await Task.Delay(10);
        
        // Assert
        sut.LoadingText.ShouldContain(errorMessage);
        sut.IsLoading.ShouldBeTrue();
    }
}
