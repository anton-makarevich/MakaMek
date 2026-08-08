using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Transport.Relay;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MakaMek.Services;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly IFileCachingService _fileCachingService = Substitute.For<IFileCachingService>();
    private readonly IUnitCachingService _unitCachingService = Substitute.For<IUnitCachingService>();
    private readonly ITerrainAssetService _terrainAssetService = Substitute.For<ITerrainAssetService>();
    private readonly IRelayHubConfigurationProvider _hubConfigurationProvider = Substitute.For<IRelayHubConfigurationProvider>();
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();
    private ILogger<SettingsViewModel> _logger = null!;
    private SettingsViewModel _sut = null!;

    private static async Task WaitFor(Func<bool> condition, int timeoutMs = 1000, int intervalMs = 50)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if ((DateTime.UtcNow - start).TotalMilliseconds > timeoutMs)
                throw new TimeoutException("Condition not met within timeout");
            await Task.Delay(intervalMs);
        }
    }

    private void CreateSut()
    {
        _logger = Substitute.For<ILogger<SettingsViewModel>>();
        _sut = new SettingsViewModel(
            _fileCachingService,
            _unitCachingService,
            _terrainAssetService,
            _localizationService,
            _hubConfigurationProvider,
            _logger);
    }

    [Fact]
    public void Constructor_ShouldInitializeClearCacheCommand()
    {
        // Arrange
        CreateSut();

        // Assert
        _sut.ClearCacheCommand.ShouldNotBeNull();
    }

    [Fact]
    public void DataSectionTitle_ShouldReturnLocalizedString()
    {
        // Arrange
        CreateSut();

        // Act
        var result = _sut.DataSectionTitle;

        // Assert
        result.ShouldBe("Data");
    }

    [Fact]
    public void ClearCacheButton_ShouldReturnLocalizedString()
    {
        // Arrange
        CreateSut();

        // Act
        var result = _sut.ClearCacheButton;

        // Assert
        result.ShouldBe("Clear Cache");
    }

    [Fact]
    public void ClearCacheDescription_ShouldReturnLocalizedString()
    {
        // Arrange
        CreateSut();

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
        CreateSut();

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
        CreateSut();

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
        CreateSut();

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
        CreateSut();

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
        CreateSut();

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
        CreateSut();

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
        CreateSut();

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
            _hubConfigurationProvider,
            logger);

        // Assert - Poll for SafeFireAndForget completion
        await WaitFor(() => logger.ReceivedCalls().Any(), timeoutMs: 1000);
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
        var logger = Substitute.For<ILogger<SettingsViewModel>>();

        // Act
        var viewModel = new SettingsViewModel(
            _fileCachingService,
            _unitCachingService,
            _terrainAssetService,
            _localizationService,
            _hubConfigurationProvider,
            logger);

        // Assert - Poll for async initialization
        await WaitFor(() => logger.ReceivedCalls().Any(), timeoutMs: 1000);
        logger.Received(1).Log(
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
        var logger = Substitute.For<ILogger<SettingsViewModel>>();

        // Act
        var viewModel = new SettingsViewModel(
            _fileCachingService,
            _unitCachingService,
            _terrainAssetService,
            _localizationService,
            _hubConfigurationProvider,
            logger);

        // Assert - Poll for async initialization
        await WaitFor(() => logger.ReceivedCalls().Any(), timeoutMs: 1000);
        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to initialize cache status")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()!);
        viewModel.CacheStatus.ShouldBe("Loaded units: {0}, Loaded biomes: {1}");
    }

    private static HubConfigData DemoHub => new("demo", "Demo Hub", "http://demo.local", string.Empty, true);
    private static HubConfigData CustomHub => new("custom-1", "My Hub", "http://my-hub.example", "secret", false);

    private void SetupProviderHubs(IReadOnlyList<HubConfigData> hubs, string activeHubId)
    {
        _hubConfigurationProvider.EnsureLoadedAsync().Returns(Task.CompletedTask);
        _hubConfigurationProvider.Hubs.Returns(hubs);
        _hubConfigurationProvider.ActiveHubId.Returns(activeHubId);
    }

    [Fact]
    public void Constructor_ShouldInitializeHubCommands()
    {
        // Arrange
        CreateSut();

        // Assert
        _sut.AddHubCommand.ShouldNotBeNull();
        _sut.RemoveHubCommand.ShouldNotBeNull();
    }

    [Fact]
    public void HubSectionTitle_ShouldReturnLocalizedString()
    {
        // Arrange
        CreateSut();

        // Act
        var result = _sut.HubSectionTitle;

        // Assert
        result.ShouldBe("Relay Hub");
    }

    [Fact]
    public void HubSelectLabel_ShouldReturnLocalizedString()
    {
        // Arrange
        CreateSut();

        // Act
        var result = _sut.HubSelectLabel;

        // Assert
        result.ShouldBe("Active hub");
    }

    [Fact]
    public void HubAddHubLabel_ShouldReturnLocalizedString()
    {
        // Arrange
        CreateSut();

        // Act
        var result = _sut.HubAddHubLabel;

        // Assert
        result.ShouldBe("Add Hub");
    }

    [Fact]
    public async Task AttachHandlers_ShouldLoadHubsAndSelectActiveHub()
    {
        // Arrange
        SetupProviderHubs([DemoHub, CustomHub], "demo");
        CreateSut();

        // Act
        _sut.AttachHandlers();
        await WaitFor(() => _sut.Hubs.Count == 2);

        // Assert
        _sut.Hubs.Count.ShouldBe(2);
        _sut.SelectedHub.ShouldNotBeNull();
        _sut.SelectedHub!.Id.ShouldBe("demo");
        _sut.Hubs[0].IsBuiltIn.ShouldBeTrue();
        _sut.Hubs[1].IsNew.ShouldBeFalse();
        _sut.Hubs[1].CanEdit.ShouldBeTrue();
        _sut.Hubs[1].CanRemove.ShouldBeTrue();
    }

    [Fact]
    public async Task SelectedHub_WhenChanged_ShouldPersistSelection()
    {
        // Arrange
        SetupProviderHubs([DemoHub, CustomHub], "demo");
        CreateSut();
        _sut.AttachHandlers();
        await WaitFor(() => _sut.Hubs.Count == 2);

        // Act
        _sut.SelectedHub = _sut.Hubs.First(h => h.Id == "custom-1");

        // Assert
        await WaitFor(() => _hubConfigurationProvider.ReceivedCalls()
            .Any(c => c.GetMethodInfo().Name == nameof(IRelayHubConfigurationProvider.SelectHubAsync)));
        await _hubConfigurationProvider.Received(1).SelectHubAsync("custom-1");
    }

    [Fact]
    public async Task AddHubCommand_WhenExecuted_ShouldAddNewEditingEntry()
    {
        // Arrange
        CreateSut();

        // Act
        await ((IAsyncCommand)_sut.AddHubCommand).ExecuteAsync();

        // Assert
        _sut.Hubs.Count.ShouldBe(1);
        var entry = _sut.Hubs[0];
        entry.IsNew.ShouldBeTrue();
        entry.IsEditing.ShouldBeTrue();
        entry.IsBuiltIn.ShouldBeFalse();
    }

    [Fact]
    public async Task AddHub_WhenCancelled_ShouldRemoveEntry()
    {
        // Arrange
        CreateSut();
        await ((IAsyncCommand)_sut.AddHubCommand).ExecuteAsync();
        var entry = _sut.Hubs.Single();

        // Act
        await ((IAsyncCommand)entry.CancelCommand).ExecuteAsync();

        // Assert
        _sut.Hubs.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddHub_WhenSaved_ShouldAddHubToProvider()
    {
        // Arrange
        SetupProviderHubs([], "demo");
        CreateSut();
        await ((IAsyncCommand)_sut.AddHubCommand).ExecuteAsync();
        var entry = _sut.Hubs.Single();
        entry.EditableName = "My Hub";
        entry.EditableBaseUrl = "http://my-hub.example";
        entry.EditableApiKey = "secret";

        // Act
        await ((IAsyncCommand)entry.SaveCommand).ExecuteAsync();

        // Assert
        await _hubConfigurationProvider.Received(1).AddHubAsync(Arg.Is<HubConfigData>(h =>
            h.Id == entry.Id && h.Name == "My Hub" && h.BaseUrl == "http://my-hub.example" && h.ApiKey == "secret" && !h.IsBuiltIn));
    }

    [Fact]
    public async Task AddHub_WhenSavedWithoutBaseUrl_ShouldNotCommit()
    {
        // Arrange
        CreateSut();
        await ((IAsyncCommand)_sut.AddHubCommand).ExecuteAsync();
        var entry = _sut.Hubs.Single();
        entry.EditableBaseUrl = "   ";

        // Act
        await ((IAsyncCommand)entry.SaveCommand).ExecuteAsync();

        // Assert
        await _hubConfigurationProvider.DidNotReceive().AddHubAsync(Arg.Any<HubConfigData>());
    }

    [Fact]
    public async Task EditExistingHub_WhenSaved_ShouldUpdateHubInProvider()
    {
        // Arrange
        SetupProviderHubs([DemoHub, CustomHub], "demo");
        CreateSut();
        _sut.AttachHandlers();
        await WaitFor(() => _sut.Hubs.Count == 2);
        var entry = _sut.Hubs.First(h => h.Id == "custom-1");

        // Act
        await ((IAsyncCommand)entry.StartEditingCommand).ExecuteAsync();
        entry.EditableName = "Renamed";
        entry.EditableBaseUrl = "http://new.example";
        entry.EditableApiKey = "new-key";
        await ((IAsyncCommand)entry.SaveCommand).ExecuteAsync();

        // Assert
        await _hubConfigurationProvider.Received(1).UpdateHubAsync("custom-1", "Renamed", "http://new.example", "new-key");
    }

    [Fact]
    public async Task EditExistingHub_WhenCancelled_ShouldRestoreEditableValues()
    {
        // Arrange
        SetupProviderHubs([DemoHub, CustomHub], "demo");
        CreateSut();
        _sut.AttachHandlers();
        await WaitFor(() => _sut.Hubs.Count == 2);
        var entry = _sut.Hubs.First(h => h.Id == "custom-1");
        await ((IAsyncCommand)entry.StartEditingCommand).ExecuteAsync();
        entry.EditableName = "Renamed";

        // Act
        await ((IAsyncCommand)entry.CancelCommand).ExecuteAsync();

        // Assert
        entry.IsEditing.ShouldBeFalse();
        entry.EditableName.ShouldBe("My Hub");
        entry.EditableBaseUrl.ShouldBe("http://my-hub.example");
        await _hubConfigurationProvider.DidNotReceive().UpdateHubAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RemoveHubCommand_WhenExecuted_ShouldRemoveHubFromProvider()
    {
        // Arrange
        SetupProviderHubs([DemoHub, CustomHub], "demo");
        CreateSut();
        _sut.AttachHandlers();
        await WaitFor(() => _sut.Hubs.Count == 2);
        var entry = _sut.Hubs.First(h => h.Id == "custom-1");

        // Act
        await ((IAsyncCommand<HubEntryViewModel>)_sut.RemoveHubCommand).ExecuteAsync(entry);

        // Assert
        await _hubConfigurationProvider.Received(1).RemoveHubAsync("custom-1");
    }

    [Fact]
    public async Task RemoveHubCommand_WhenBuiltIn_ShouldNotRemove()
    {
        // Arrange
        SetupProviderHubs([DemoHub], "demo");
        CreateSut();
        _sut.AttachHandlers();
        await WaitFor(() => _sut.Hubs.Count == 1);
        var entry = _sut.Hubs.Single();

        // Act
        await ((IAsyncCommand<HubEntryViewModel>)_sut.RemoveHubCommand).ExecuteAsync(entry);

        // Assert
        await _hubConfigurationProvider.DidNotReceive().RemoveHubAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task BuiltInHub_ShouldNotAllowEditing()
    {
        // Arrange
        CreateSut();
        var entry = new HubEntryViewModel(DemoHub);

        // Act
        await ((IAsyncCommand)entry.StartEditingCommand).ExecuteAsync();

        // Assert
        entry.IsEditing.ShouldBeFalse();
    }
}
