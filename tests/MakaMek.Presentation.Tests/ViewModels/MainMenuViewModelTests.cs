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
    private readonly IUnitCachingService _unitCachingService = Substitute.For<IUnitCachingService>();

    public MainMenuViewModelTests()
    {
        _navigationService = Substitute.For<INavigationService>();
        
        // Setup default behavior for unit caching service
        _unitCachingService.GetAvailableModels().Returns(["LCT-1V", "SHD-2D"]);

        _sut = new MainMenuViewModel(_unitCachingService, 0);
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
        const string errorMessage = "Test error message";
        _unitCachingService.When(x => x.GetAvailableModels())
            .Throw(new Exception(errorMessage));
            
        _sut.SetNavigationService(_navigationService);
        
        // Small delay to allow the background task to complete
        await Task.Delay(10);
        
        // Assert
        _sut.LoadingText.ShouldContain(errorMessage);
        _sut.IsLoading.ShouldBeTrue();
    }
    
    [Fact]
    public async Task PreloadUnits_WhenNoUnitsFound_SetsNoItemsFoundTextAndKeepsLoadingTrue()
    {
        // Arrange
        _unitCachingService.GetAvailableModels().Returns([]);
        
        _sut.SetNavigationService(_navigationService);
        
        // Small delay to allow the background task to complete
        await Task.Delay(10);
        
        // Assert
        _sut.LoadingText.ShouldContain("No items found");
        _sut.IsLoading.ShouldBeTrue();
    }   
}
