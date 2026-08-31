using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Services;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class AssetLoadingViewModelTests
{
    private readonly AssetLoadingViewModel _sut;
    private readonly IUnitCachingService _unitCachingService = Substitute.For<IUnitCachingService>();
    private readonly ITerrainAssetService _terrainAssetService = Substitute.For<ITerrainAssetService>();
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IDispatcherService _dispatcherService;
    private readonly ILogger _logger = Substitute.For<ILogger>();

    public AssetLoadingViewModelTests()
    {
        _dispatcherService = Substitute.For<IDispatcherService>();
        _dispatcherService.When(x => x.RunOnUIThread(Arg.Any<Action>()))
            .Do(ci => ci.Arg<Action>()());

        _unitCachingService.GetAvailableModels().Returns(["LCT-1V", "SHD-2D"]);
        _terrainAssetService.GetLoadedBiomes().Returns(["grassland", "desert"]);

        _localizationService.GetString("MainMenu_Loading_Units").Returns("Loading units...");
        _localizationService.GetString("MainMenu_Loading_Biomes").Returns("Loading biomes...");
        _localizationService.GetString("MainMenu_Loading_UnitsProgress").Returns("Loading units {0}/{1}");
        _localizationService.GetString("MainMenu_Loading_BiomesProgress").Returns("Loading biomes {0}/{1}");
        _localizationService.GetString("MainMenu_Loading_NoUnitsFound").Returns("No units found");
        _localizationService.GetString("MainMenu_Loading_NoBiomesFound").Returns("No biomes found");
        _localizationService.GetString("MainMenu_Loading_UnitsLoaded").Returns("Loaded {0} units");
        _localizationService.GetString("MainMenu_Loading_BiomesLoaded").Returns("Loaded {0} biomes");
        _localizationService.GetString("MainMenu_Loading_UnitsError").Returns("Error loading units: {0}");
        _localizationService.GetString("MainMenu_Loading_BiomesError").Returns("Error loading biomes: {0}");

        _sut = new AssetLoadingViewModel(_unitCachingService, _terrainAssetService, _localizationService, _dispatcherService, _logger);
    }

    [Fact]
    public void InitializeAsync_SetsIsLoadingTrue()
    {
        _sut.IsLoading.ShouldBeFalse();
        _sut.InitializeAsync();
        _sut.IsLoading.ShouldBeTrue();
    }

    [Fact]
    public async Task InitializeAsync_CompletesSuccessfully_SetsIsLoadingFalse()
    {
        _sut.InitializeAsync(0);

        // Wait for loading to complete
        for (var i = 0; i < 100 && _sut.IsLoading; i++)
            await Task.Delay(10);

        _sut.IsLoading.ShouldBeFalse();
    }

    [Fact]
    public void UnitLoadProgress_UpdatesLoadingTextAndLoadingProgress()
    {
        _sut.InitializeAsync(0);

        _unitCachingService.LoadProgress += Raise.Event<EventHandler<ResourceLoadProgressEventArgs>>(
            new ResourceLoadProgressEventArgs(2, 5));

        _sut.LoadingText.ShouldContain("Loading units 2/5");
        _sut.LoadingProgress.ShouldBe(2d / 5);
    }

    [Fact]
    public void BiomeLoadProgress_UpdatesLoadingTextAndLoadingProgress()
    {
        _sut.InitializeAsync(0);

        _terrainAssetService.LoadProgress += Raise.Event<EventHandler<ResourceLoadProgressEventArgs>>(
            new ResourceLoadProgressEventArgs(1, 4));

        _sut.LoadingText.ShouldContain("Loading biomes 1/4");
        _sut.LoadingProgress.ShouldBe(1d / 4);
    }

    [Fact]
    public void LoadingProgress_IsSetToOne_WhenBothPreloadsComplete()
    {
        _sut.InitializeAsync(0);

        _unitCachingService.LoadProgress += Raise.Event<EventHandler<ResourceLoadProgressEventArgs>>(
            new ResourceLoadProgressEventArgs(5, 5));
        _terrainAssetService.LoadProgress += Raise.Event<EventHandler<ResourceLoadProgressEventArgs>>(
            new ResourceLoadProgressEventArgs(10, 10));

        _sut.LoadingProgress.ShouldBe(1);
    }

    [Fact]
    public void LoadingProgress_ReflectsBothPreloads_WhenOneCompletesBeforeOther()
    {
        _sut.InitializeAsync(0);

        _unitCachingService.LoadProgress += Raise.Event<EventHandler<ResourceLoadProgressEventArgs>>(
            new ResourceLoadProgressEventArgs(5, 5));
        _terrainAssetService.LoadProgress += Raise.Event<EventHandler<ResourceLoadProgressEventArgs>>(
            new ResourceLoadProgressEventArgs(2, 10));

        _sut.LoadingProgress.ShouldBe((5d + 2d) / (5d + 10d));
        _sut.LoadingProgress.ShouldNotBe(1);
    }

    [Fact]
    public async Task PreloadUnits_WhenExceptionThrown_SetsErrorTextAndKeepsLoadingTrue()
    {
        const string errorMessage = "Test error message";
        _unitCachingService
            .GetAvailableModels()
            .Returns(Task.FromException<IEnumerable<string>>(new Exception(errorMessage)));

        var sut = new AssetLoadingViewModel(_unitCachingService, _terrainAssetService, _localizationService, _dispatcherService, _logger);
        sut.InitializeAsync(0);

        for (var i = 0; i < 100 && sut.IsLoading && !sut.LoadingText.Contains(errorMessage); i++)
            await Task.Delay(10);

        sut.LoadingText.ShouldContain(errorMessage);
        sut.IsLoading.ShouldBeTrue();
        sut.HasError.ShouldBeTrue();
    }

    [Fact]
    public async Task PreloadUnits_WhenNoUnitsFound_SetsNoItemsFoundTextAndKeepsLoadingTrue()
    {
        _unitCachingService.GetAvailableModels().Returns([]);

        var sut = new AssetLoadingViewModel(_unitCachingService, _terrainAssetService, _localizationService, _dispatcherService, _logger);
        sut.InitializeAsync(0);

        for (var i = 0; i < 100 && sut.IsLoading && !sut.LoadingText.Contains("No units found"); i++)
            await Task.Delay(10);

        sut.LoadingText.ShouldContain("No units found");
        sut.IsLoading.ShouldBeTrue();
    }

    [Fact]
    public async Task PreloadBiomes_WhenExceptionThrown_SetsErrorTextAndKeepsLoadingTrue()
    {
        const string errorMessage = "Test error message";
        _terrainAssetService
            .GetLoadedBiomes()
            .Returns(Task.FromException<IEnumerable<string>>(new Exception(errorMessage)));

        var sut = new AssetLoadingViewModel(_unitCachingService, _terrainAssetService, _localizationService, _dispatcherService, _logger);
        sut.InitializeAsync(0);

        for (var i = 0; i < 100 && sut.IsLoading && !sut.LoadingText.Contains(errorMessage); i++)
            await Task.Delay(10);

        sut.LoadingText.ShouldContain(errorMessage);
        sut.IsLoading.ShouldBeTrue();
        sut.HasError.ShouldBeTrue();
    }

    [Fact]
    public async Task PreloadBiomes_WhenNoBiomesFound_SetsNoBiomesFoundTextAndKeepsLoadingTrue()
    {
        _terrainAssetService.GetLoadedBiomes().Returns([]);

        var sut = new AssetLoadingViewModel(_unitCachingService, _terrainAssetService, _localizationService, _dispatcherService, _logger);
        sut.InitializeAsync(0);

        for (var i = 0; i < 100 && sut.IsLoading && !sut.LoadingText.Contains("No biomes found"); i++)
            await Task.Delay(10);

        sut.LoadingText.ShouldContain("No biomes found");
        sut.IsLoading.ShouldBeTrue();
    }

    [Fact]
    public async Task ReloadAsync_ClearsCacheAndReinitializes()
    {
        _sut.InitializeAsync(0);

        // Wait for initial load
        for (var i = 0; i < 100 && _sut.IsLoading; i++)
            await Task.Delay(10);

        _sut.IsLoading.ShouldBeFalse();

        // Act
        await _sut.ReloadAsync();

        // Assert
        _unitCachingService.Received(1).ClearCache();
        _terrainAssetService.Received(1).ClearCache();
        _sut.IsLoading.ShouldBeTrue();
    }
}
