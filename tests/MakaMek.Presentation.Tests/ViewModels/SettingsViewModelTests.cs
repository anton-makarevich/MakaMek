using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Presentation.ViewModels;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly IFileCachingService _fileCachingService = Substitute.For<IFileCachingService>();
    private readonly IUnitCachingService _unitCachingService = Substitute.For<IUnitCachingService>();
    private readonly ITerrainAssetService _terrainAssetService = Substitute.For<ITerrainAssetService>();
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly SettingsViewModel _sut;

    public SettingsViewModelTests()
    {
        _logger = Substitute.For<ILogger<SettingsViewModel>>();

        _sut = new SettingsViewModel(
            _fileCachingService,
            _unitCachingService,
            _terrainAssetService,
            _localizationService,
            _logger);
    }

    [Fact]
    public void Constructor_ShouldInitializeClearCacheCommand()
    {
        // Assert
        _sut.ClearCacheCommand.ShouldNotBeNull();
    }

    [Fact]
    public void DataSectionTitle_ShouldReturnLocalizedString()
    {
        // Act
        var result = _sut.DataSectionTitle;

        // Assert
        result.ShouldBe("Data");
    }

    [Fact]
    public void ClearCacheButton_ShouldReturnLocalizedString()
    {
        // Act
        var result = _sut.ClearCacheButton;

        // Assert
        result.ShouldBe("Clear Cache");
    }

    [Fact]
    public void ClearCacheDescription_ShouldReturnLocalizedString()
    {
        // Act
        var result = _sut.ClearCacheDescription;

        // Assert
        result.ShouldContain("app restart");
        result.ShouldContain("clearing the cache");
    }

    [Fact]
    public async Task ClearCacheCommand_ShouldClearAllCaches()
    {
        // Arrange
        _unitCachingService.GetAvailableModels().Returns([]);
        _terrainAssetService.GetLoadedBiomes().Returns([]);

        // Act
        await ((IAsyncCommand)_sut.ClearCacheCommand).ExecuteAsync();

        // Assert
        await _fileCachingService.Received(1).ClearCache();
        _unitCachingService.Received(1).ClearCache();
        _terrainAssetService.Received(1).ClearCache();
    }

    [Fact]
    public async Task ClearCacheCommand_ShouldSetIsBusyToTrueDuringExecution()
    {
        // Arrange
        _fileCachingService.ClearCache().Returns(Task.Delay(100));
        _unitCachingService.GetAvailableModels().Returns([]);
        _terrainAssetService.GetLoadedBiomes().Returns([]);

        // Act
        var task = ((IAsyncCommand)_sut.ClearCacheCommand).ExecuteAsync();
        
        // Assert
        _sut.IsBusy.ShouldBeTrue();
        await task;
        _sut.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task ClearCacheCommand_ShouldUpdateCacheStatusToClearing()
    {
        // Arrange
        _fileCachingService.ClearCache().Returns(Task.Delay(100));
        _unitCachingService.GetAvailableModels().Returns([]);
        _terrainAssetService.GetLoadedBiomes().Returns([]);

        // Act
        var task = ((IAsyncCommand)_sut.ClearCacheCommand).ExecuteAsync();
        
        // Assert
        _sut.CacheStatus.ShouldBe("Clearing cache...");
        await task;
    }

    [Fact]
    public async Task ClearCacheCommand_ShouldUpdateCacheStatusToClearedAfterSuccess()
    {
        // Arrange
        _unitCachingService.GetAvailableModels().Returns([]);
        _terrainAssetService.GetLoadedBiomes().Returns([]);

        // Act
        await ((IAsyncCommand)_sut.ClearCacheCommand).ExecuteAsync();

        // Assert
        _sut.CacheStatus.ShouldBe("Cache cleared successfully");
    }

    [Fact]
    public async Task ClearCacheCommand_ShouldSetIsBusyToFalseAfterCompletion()
    {
        // Arrange
        _unitCachingService.GetAvailableModels().Returns([]);
        _terrainAssetService.GetLoadedBiomes().Returns([]);

        // Act
        await ((IAsyncCommand)_sut.ClearCacheCommand).ExecuteAsync();

        // Assert
        _sut.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task ClearCacheCommand_WhenExceptionThrown_ShouldLogError()
    {
        // Arrange
        _fileCachingService.ClearCache().Returns(Task.FromException(new Exception("Test error")));

        // Act
        await ((IAsyncCommand)_sut.ClearCacheCommand).ExecuteAsync();

        // Assert
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to clear cache")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()!);
    }

    [Fact]
    public async Task ClearCacheCommand_WhenExceptionThrown_ShouldSetIsBusyToFalse()
    {
        // Arrange
        _fileCachingService.ClearCache().Returns(Task.FromException(new Exception("Test error")));

        // Act
        await ((IAsyncCommand)_sut.ClearCacheCommand).ExecuteAsync();

        // Assert
        _sut.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task Constructor_WhenInitializeCacheStatusAsyncFails_ShouldLogError()
    {
        // Arrange
        _unitCachingService.GetAvailableModels().Returns(Task.FromException<IEnumerable<string>>(new Exception("Test error")));
        var logger = Substitute.For<ILogger<SettingsViewModel>>();

        // Act
        _ = new SettingsViewModel(
            _fileCachingService,
            _unitCachingService,
            _terrainAssetService,
            _localizationService,
            logger);

        // Assert - Give time for SafeFireAndForget to complete
        await Task.Delay(100);
        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to initialize cache status")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()!);
    }

    [Fact]
    public async Task InitializeCacheStatusAsync_WhenGetAvailableModelsThrows_ShouldLogErrorAndSetDefaultStatus()
    {
        // Arrange
        _unitCachingService.GetAvailableModels().Returns(Task.FromException<IEnumerable<string>>(new Exception("Test error")));
        _terrainAssetService.GetLoadedBiomes().Returns([]);

        // Act
        var viewModel = new SettingsViewModel(
            _fileCachingService,
            _unitCachingService,
            _terrainAssetService,
            _localizationService,
            _logger);

        // Assert - Give time for async initialization
        await Task.Delay(100);
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to initialize cache status")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()!);
        viewModel.CacheStatus.ShouldBe("Loaded units: {0}, Loaded biomes: {1}");
    }

    [Fact]
    public async Task InitializeCacheStatusAsync_WhenGetLoadedBiomesThrows_ShouldLogErrorAndSetDefaultStatus()
    {
        // Arrange
        _unitCachingService.GetAvailableModels().Returns([]);
        _terrainAssetService.GetLoadedBiomes().Returns(Task.FromException<IEnumerable<string>>(new Exception("Test error")));

        // Act
        var viewModel = new SettingsViewModel(
            _fileCachingService,
            _unitCachingService,
            _terrainAssetService,
            _localizationService,
            _logger);

        // Assert - Give time for async initialization
        await Task.Delay(100);
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to initialize cache status")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()!);
        viewModel.CacheStatus.ShouldBe("Loaded units: {0}, Loaded biomes: {1}");
    }
}
