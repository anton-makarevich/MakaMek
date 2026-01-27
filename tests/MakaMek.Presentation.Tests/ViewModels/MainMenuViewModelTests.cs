using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MVVM.Core.Services;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class MainMenuViewModelTests
{
    private readonly MainMenuViewModel _sut;
    private readonly INavigationService _navigationService;
    private readonly IUnitCachingService _unitCachingService = Substitute.For<IUnitCachingService>();
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IHashService _hashService = Substitute.For<IHashService>();
    private readonly ILogger<MainMenuViewModel> _logger = Substitute.For<ILogger<MainMenuViewModel>>();

    public MainMenuViewModelTests()
    {
        _navigationService = Substitute.For<INavigationService>();

        // Set up a default behavior for unit caching service
        _unitCachingService.GetAvailableModels().Returns(["LCT-1V", "SHD-2D"]);

        // Set up a default behavior for localization service
        _localizationService.GetString("MainMenu_Loading_Content").Returns("Loading content...");
        _localizationService.GetString("MainMenu_Loading_NoItemsFound").Returns("No items found");
        _localizationService.GetString("MainMenu_Loading_ItemsLoaded").Returns("Loaded {0} items");
        _localizationService.GetString("MainMenu_Loading_Error").Returns("Error loading units: {0}");

        _sut = new MainMenuViewModel(_unitCachingService, _localizationService, _logger, 0);
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
        var startVm = new StartNewGameViewModel(
            Substitute.For<IGameManager>(),
            Substitute.For<IUnitsLoader>(),
            Substitute.For<IRulesProvider>(),
            Substitute.For<IMechFactory>(),
            Substitute.For<ICommandPublisher>(),
            Substitute.For<IToHitCalculator>(),
            Substitute.For<IPilotingSkillCalculator>(),
            Substitute.For<IConsciousnessCalculator>(),
            Substitute.For<IHeatEffectsCalculator>(),
            Substitute.For<IDispatcherService>(),
            Substitute.For<IGameFactory>(),
            Substitute.For<IBattleMapFactory>(),
            Substitute.For<IFileCachingService>(),
            Substitute.For<IMapPreviewRenderer>(),
            _hashService,
            Substitute.For<IBotManager>(),
            Substitute.For<ILogger<StartNewGameViewModel>>()
        );
        _navigationService.GetNewViewModel<StartNewGameViewModel>().Returns(startVm);

        // Act
        await command.ExecuteAsync();

        // Assert
        await _navigationService.Received(1).NavigateToViewModelAsync(startVm);
    }
    
    [Fact]
    public async Task StartNewGameCommand_ShouldThrow_WhenNavigationServiceDoesNotReturnStartNewGameViewModel()
    {
        // Arrange
        _navigationService.GetNewViewModel<StartNewGameViewModel>().Returns((StartNewGameViewModel?)null);
        // Act & Assert
        (await Should.ThrowAsync<Exception>(async () => await ((IAsyncCommand)_sut.StartNewGameCommand)
            .ExecuteAsync())).Message.ShouldContain("StartNewGameViewModel is not registered");
    }
    
    [Fact]
    public async Task JoinGameCommand_WhenExecuted_NavigatesToJoinGameViewModel()
    {
        // Arrange
        var command = _sut.JoinGameCommand as IAsyncCommand;
        command.ShouldNotBeNull();
        var joinVm = new JoinGameViewModel(
            Substitute.For<IRulesProvider>(),
            Substitute.For<IMechFactory>(),
            Substitute.For<IUnitsLoader>(),
            Substitute.For<ICommandPublisher>(),
            Substitute.For<IToHitCalculator>(),
            Substitute.For<IPilotingSkillCalculator>(),
            Substitute.For<IConsciousnessCalculator>(),
            Substitute.For<IHeatEffectsCalculator>(),
            Substitute.For<IDispatcherService>(),
            Substitute.For<IGameFactory>(),
            Substitute.For<ITransportFactory>(),
            Substitute.For<IBattleMapFactory>(),
            Substitute.For<IFileCachingService>(),
            _hashService,
            Substitute.For<IBotManager>(),
            Substitute.For<ILogger<JoinGameViewModel>>()
        );
        _navigationService.GetNewViewModel<JoinGameViewModel>().Returns(joinVm);

        // Act
        await command.ExecuteAsync();

        // Assert
        await _navigationService.Received(1).NavigateToViewModelAsync(joinVm);
    }
    
    [Fact]
    public async Task JoinGameCommand_ShouldThrow_WhenNavigationServiceDoesNotReturnJoinGameViewModel()
    {
        // Arrange
        _navigationService.GetNewViewModel<JoinGameViewModel>().Returns((JoinGameViewModel?)null);
        // Act & Assert
        (await Should.ThrowAsync<Exception>(async () => await ((IAsyncCommand)_sut.JoinGameCommand)
            .ExecuteAsync())).Message.ShouldContain("JoinGameViewModel is not registered");
    }
    
    [Fact]
    public async Task AboutCommand_WhenExecuted_NavigatesToAboutViewModel()
    {
        // Arrange
        var command = _sut.AboutCommand as IAsyncCommand;
        command.ShouldNotBeNull();
        var aboutVm = new AboutViewModel(
            Substitute.For<IExternalNavigationService>(),
            Substitute.For<ILocalizationService>());
        _navigationService.GetNewViewModel<AboutViewModel>().Returns(aboutVm);

        // Act
        await command.ExecuteAsync();

        // Assert
        await _navigationService.Received(1).NavigateToViewModelAsync(aboutVm);
    }
    
    [Fact]
    public async Task AboutCommand_ShouldThrow_WhenNavigationServiceDoesNotReturnAboutViewModel()
    {
        // Arrange
        _navigationService.GetNewViewModel<AboutViewModel>().Returns((AboutViewModel?)null);
        // Act & Assert
        (await Should.ThrowAsync<Exception>(async () => await ((IAsyncCommand)_sut.AboutCommand)
            .ExecuteAsync())).Message.ShouldContain("AboutViewModel is not registered");
    }   
    
    [Fact]
    public async Task PreloadUnits_WhenExceptionThrown_SetsErrorTextAndKeepsLoadingTrue()
    {
        // Arrange
        const string errorMessage = "Test error message";
        _unitCachingService
            .GetAvailableModels()
            .Returns(Task.FromException<IEnumerable<string>>(new Exception(errorMessage)));
        
        var sut = new MainMenuViewModel(_unitCachingService, _localizationService, _logger, 0);
        sut.SetNavigationService(_navigationService);
        
        // Small delay to allow the background task to complete
        for (var i = 0; i < 100 && sut.LoadingText == "Loading content..."; i++)
            await Task.Delay(10);
        
        // Assert
        sut.LoadingText.ShouldContain(errorMessage);
        sut.IsLoading.ShouldBeTrue();
    }
    
    [Fact]
    public async Task PreloadUnits_WhenNoUnitsFound_SetsNoItemsFoundTextAndKeepsLoadingTrue()
    {
        // Arrange
        _unitCachingService.GetAvailableModels().Returns([]);
        
        var sut = new MainMenuViewModel(_unitCachingService, _localizationService, _logger, 0);
        sut.SetNavigationService(_navigationService);
        
        // Small delay to allow the background task to complete
        for (var i = 0; i < 100 && sut.LoadingText == "Loading content..."; i++)
            await Task.Delay(10);
        
        // Assert
        sut.LoadingText.ShouldContain("No items found");
        sut.IsLoading.ShouldBeTrue();
    }   
}
