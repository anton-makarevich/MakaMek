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
    private readonly IUnitCachingService _unitCachingService;

    public MainMenuViewModelTests()
    {
        _navigationService = Substitute.For<INavigationService>();
        _unitCachingService = Substitute.For<IUnitCachingService>();

        // Setup default behavior for unit caching service
        _unitCachingService.GetAvailableModels().Returns(new[] { "LCT-1V", "SHD-2D" });

        _sut = new MainMenuViewModel(_unitCachingService);
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
    public void Constructor_InitializesLoadingState()
    {
        // Assert
        _sut.IsLoading.ShouldBeTrue(); // Should start in loading state
        _sut.LoadingText.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenUnitCachingServiceIsNull()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => new MainMenuViewModel(null!));
    }

    [Fact]
    public async Task Commands_ShouldBeDisabled_WhenLoading()
    {
        // Arrange - Create a new instance that will be in loading state
        var unitCachingService = Substitute.For<IUnitCachingService>();
        unitCachingService.GetAvailableModels().Returns(Task.FromResult<IEnumerable<string>>(new[] { "LCT-1V" }));

        var viewModel = new MainMenuViewModel(unitCachingService);

        // Assert
        viewModel.IsLoading.ShouldBeTrue();
        viewModel.StartNewGameCommand.CanExecute(null).ShouldBeFalse();
        viewModel.JoinGameCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task Commands_ShouldBeEnabled_AfterLoadingCompletes()
    {
        // Arrange
        var unitCachingService = Substitute.For<IUnitCachingService>();
        unitCachingService.GetAvailableModels().Returns(Task.FromResult<IEnumerable<string>>(new[] { "LCT-1V" }));

        var viewModel = new MainMenuViewModel(unitCachingService);

        // Wait for loading to complete
        await Task.Delay(1500); // Wait for preloading + delay

        // Assert
        viewModel.IsLoading.ShouldBeFalse();
        viewModel.StartNewGameCommand.CanExecute(null).ShouldBeTrue();
        viewModel.JoinGameCommand.CanExecute(null).ShouldBeTrue();
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
