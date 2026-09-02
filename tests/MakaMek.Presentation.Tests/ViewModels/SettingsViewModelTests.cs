using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sanet.MakaMek.Assets.Configuration;
using Sanet.MakaMek.Assets.Services;
using Sanet.Transport.SignalR.Client.Relay;
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
    private readonly IRelayRoomClient _relayRoomClient = Substitute.For<IRelayRoomClient>();
    private readonly IAssetProviderConfigurationProvider _assetProviderConfigurationProvider = Substitute.For<IAssetProviderConfigurationProvider>();
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();
    private readonly IDispatcherService _dispatcherService = Substitute.For<IDispatcherService>();
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

    private void CreateSut(ILogger<SettingsViewModel>? logger = null)
    {
        _logger = logger ?? Substitute.For<ILogger<SettingsViewModel>>();
        var assetLoadingViewModel = new AssetLoadingViewModel(
            _unitCachingService,
            _terrainAssetService,
            _localizationService,
            _dispatcherService,
            Substitute.For<ILogger>());
        _sut = new SettingsViewModel(
            _fileCachingService,
            _unitCachingService,
            _terrainAssetService,
            _localizationService,
            _hubConfigurationProvider,
            _relayRoomClient,
            _assetProviderConfigurationProvider,
            assetLoadingViewModel,
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
        await _unitCachingService.Received(1).ClearCache();
        await _terrainAssetService.Received(1).ClearCache();
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
        CreateSut(logger);

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
        CreateSut(logger);
        var viewModel = _sut;

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
        CreateSut(logger);
        var viewModel = _sut;

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
        _hubConfigurationProvider.GetHubs().Returns(Task.FromResult(hubs));
        _hubConfigurationProvider.GetActiveHubId().Returns(Task.FromResult(activeHubId));
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
            .Any(c => c.GetMethodInfo().Name == nameof(IRelayHubConfigurationProvider.SelectHub)));
        await _hubConfigurationProvider.Received(1).SelectHub("custom-1");
    }

    [Fact]
    public async Task SelectedHub_ConsecutiveSelections_PersistInSelectionOrder_EvenIfFirstCompletesLate()
    {
        // Arrange
        SetupProviderHubs([DemoHub, CustomHub], "demo");
        CreateSut();
        _sut.AttachHandlers();
        await WaitFor(() => _sut.Hubs.Count == 2);

        var firstSelection = new TaskCompletionSource();
        _hubConfigurationProvider.SelectHub("custom-1").Returns(firstSelection.Task);
        _hubConfigurationProvider.SelectHub("demo").Returns(Task.CompletedTask);

        // Act - select custom-1 first, then demo while the first write is still in flight
        _sut.SelectedHub = _sut.Hubs.First(h => h.Id == "custom-1");
        await WaitFor(() => _hubConfigurationProvider.ReceivedCalls()
            .Any(c => c.GetMethodInfo().Name == nameof(IRelayHubConfigurationProvider.SelectHub)));
        _sut.SelectedHub = _sut.Hubs.First(h => h.Id == "demo");

        // Assert - the second selection must wait for the first one to finish
        await _hubConfigurationProvider.Received(1).SelectHub("custom-1");
        await _hubConfigurationProvider.DidNotReceive().SelectHub("demo");

        // Complete the first selection; only then is the second issued
        firstSelection.SetResult();
        await WaitFor(() => _hubConfigurationProvider.ReceivedCalls().Any(c =>
            c.GetMethodInfo().Name == nameof(IRelayHubConfigurationProvider.SelectHub)
            && c.GetArguments()[0] as string == "demo"));

        Received.InOrder(() =>
        {
            _hubConfigurationProvider.SelectHub("custom-1");
            _hubConfigurationProvider.SelectHub("demo");
        });
    }

    [Fact]
    public async Task SelectedHub_WhenEarlierSelectionFails_LaterSelectionStillPersists()
    {
        // Arrange
        SetupProviderHubs([DemoHub, CustomHub], "demo");
        CreateSut();
        _sut.AttachHandlers();
        await WaitFor(() => _sut.Hubs.Count == 2);

        var firstSelection = new TaskCompletionSource();
        _hubConfigurationProvider.SelectHub("custom-1").Returns(firstSelection.Task);
        _hubConfigurationProvider.SelectHub("demo").Returns(Task.CompletedTask);

        // Act - select custom-1 first, then demo while the first write is still in flight
        _sut.SelectedHub = _sut.Hubs.First(h => h.Id == "custom-1");
        await WaitFor(() => _hubConfigurationProvider.ReceivedCalls()
            .Any(c => c.GetMethodInfo().Name == nameof(IRelayHubConfigurationProvider.SelectHub)));
        _sut.SelectedHub = _sut.Hubs.First(h => h.Id == "demo");

        // The second selection must wait for the first one to finish
        await _hubConfigurationProvider.Received(1).SelectHub("custom-1");
        await _hubConfigurationProvider.DidNotReceive().SelectHub("demo");

        // Fail the first selection; the later selection must still go through
        firstSelection.SetException(new Exception("selection failed"));
        await WaitFor(() => _hubConfigurationProvider.ReceivedCalls().Any(c =>
            c.GetMethodInfo().Name == nameof(IRelayHubConfigurationProvider.SelectHub)
            && c.GetArguments()[0] as string == "demo"));

        await _hubConfigurationProvider.Received(1).SelectHub("demo");
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
        await _hubConfigurationProvider.Received(1).AddHub(Arg.Is<HubConfigData>(h =>
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
        await _hubConfigurationProvider.DidNotReceive().AddHub(Arg.Any<HubConfigData>());
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
        await _hubConfigurationProvider.Received(1).UpdateHub("custom-1", "Renamed", "http://new.example", "new-key");
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
        await _hubConfigurationProvider.DidNotReceive().UpdateHub(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task AddHub_WhenPersistenceFails_KeepsEditorOpenAndDoesNotCommit()
    {
        // Arrange
        _hubConfigurationProvider.AddHub(Arg.Any<HubConfigData>())
            .ThrowsAsync(new Exception("persist failed"));
        CreateSut();
        await ((IAsyncCommand)_sut.AddHubCommand).ExecuteAsync();
        var entry = _sut.Hubs.Single();
        entry.EditableName = "My Hub";
        entry.EditableBaseUrl = "http://my-hub.example";
        entry.EditableApiKey = "secret";

        // Act & Assert
        await Should.ThrowAsync<Exception>(() => ((IAsyncCommand)entry.SaveCommand).ExecuteAsync());

        // The editor stays open with the edited values so the save can be retried
        entry.IsEditing.ShouldBeTrue();
        entry.EditableName.ShouldBe("My Hub");
        entry.EditableBaseUrl.ShouldBe("http://my-hub.example");
        entry.EditableApiKey.ShouldBe("secret");
        // The hub itself is not committed
        entry.Hub.Name.ShouldBeEmpty();
        entry.Hub.BaseUrl.ShouldBeEmpty();
    }

    [Fact]
    public async Task EditExistingHub_WhenPersistenceFails_KeepsEditorOpenAndDoesNotCommit()
    {
        // Arrange
        SetupProviderHubs([DemoHub, CustomHub], "demo");
        _hubConfigurationProvider.UpdateHub(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new Exception("persist failed"));
        CreateSut();
        _sut.AttachHandlers();
        await WaitFor(() => _sut.Hubs.Count == 2);
        var entry = _sut.Hubs.First(h => h.Id == "custom-1");
        await ((IAsyncCommand)entry.StartEditingCommand).ExecuteAsync();
        entry.EditableName = "Renamed";
        entry.EditableBaseUrl = "http://new.example";
        entry.EditableApiKey = "new-key";

        // Act & Assert
        await Should.ThrowAsync<Exception>(() => ((IAsyncCommand)entry.SaveCommand).ExecuteAsync());

        // The editor stays open with the edited values so the save can be retried
        entry.IsEditing.ShouldBeTrue();
        entry.EditableName.ShouldBe("Renamed");
        entry.EditableBaseUrl.ShouldBe("http://new.example");
        entry.EditableApiKey.ShouldBe("new-key");
        // The committed hub still holds the previous values
        entry.Name.ShouldBe("My Hub");
        entry.BaseUrl.ShouldBe("http://my-hub.example");
        entry.ApiKey.ShouldBe("secret");
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
        await _hubConfigurationProvider.Received(1).RemoveHub("custom-1");
    }

    [Fact]
    public async Task RemoveHubCommand_WhenExecutedWithNull_ShouldNotRemove()
    {
        // Arrange
        CreateSut();

        // Act
        await ((IAsyncCommand<HubEntryViewModel>)_sut.RemoveHubCommand).ExecuteAsync(null!);

        // Assert
        await _hubConfigurationProvider.DidNotReceive().RemoveHub(Arg.Any<string>());
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
        await _hubConfigurationProvider.DidNotReceive().RemoveHub(Arg.Any<string>());
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

    [Fact]
    public async Task AttachHandlers_ShouldProbeStatusForEachHub()
    {
        // Arrange
        SetupProviderHubs([DemoHub, CustomHub], "demo");
        _relayRoomClient.Health(Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions>())
            .Returns((RelayClientError?)null);
        CreateSut();

        // Act
        _sut.AttachHandlers();
        await WaitFor(() => _sut.Hubs.Count == 2);
        await WaitFor(() => _relayRoomClient.ReceivedCalls()
            .Any(c => c.GetMethodInfo().Name == nameof(IRelayRoomClient.Health)));

        // Assert
        await _relayRoomClient.Received(2).Health(Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions>());
        await WaitFor(() => _sut.Hubs.All(h => h.Status == HubStatus.Online));
    }

    [Fact]
    public async Task LoadHubs_Probe_UsesHubBaseUrlAndApiKey()
    {
        // Arrange
        SetupProviderHubs([CustomHub], "custom-1");
        _relayRoomClient.Health(Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions>())
            .Returns((RelayClientError?)null);
        CreateSut();

        // Act
        _sut.AttachHandlers();
        await WaitFor(() => _sut.Hubs.Count == 1);
        await WaitFor(() => _relayRoomClient.ReceivedCalls()
            .Any(c => c.GetMethodInfo().Name == nameof(IRelayRoomClient.Health)));

        // Assert
        await _relayRoomClient.Received(1).Health(
            Arg.Any<CancellationToken>(),
            Arg.Is<RelayClientOptions>(o =>
                o.BaseUrl == CustomHub.BaseUrl && o.ApiKey == CustomHub.ApiKey));
    }

    [Fact]
    public async Task LoadHubs_WhenProbeThrows_LogsErrorAndMarksOffline()
    {
        // Arrange
        SetupProviderHubs([CustomHub], "custom-1");
        _relayRoomClient.Health(Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions>())
            .Returns(Task.FromException<RelayClientError?>(new Exception("probe failed")));
        CreateSut();

        // Act
        _sut.AttachHandlers();
        await WaitFor(() => _sut.Hubs.Count == 1);
        var entry = _sut.Hubs.Single();
        await WaitFor(() => entry.Status == HubStatus.Offline);

        // Assert
        entry.Status.ShouldBe(HubStatus.Offline);
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("probe failed")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()!);
    }

    [Fact]
    public async Task AddHub_WhenSaved_ShouldProbeStatusForPersistedRow()
    {
        // Arrange
        var hubs = new List<HubConfigData> { DemoHub };
        _hubConfigurationProvider.GetHubs()
            .Returns(_ => Task.FromResult<IReadOnlyList<HubConfigData>>(hubs.ToArray()));
        _hubConfigurationProvider.GetActiveHubId().Returns(Task.FromResult("demo"));
        _hubConfigurationProvider.AddHub(Arg.Any<HubConfigData>())
            .Returns(Task.CompletedTask)
            .AndDoes(callInfo => hubs.Add(callInfo.Arg<HubConfigData>()));
        _relayRoomClient.Health(Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions>())
            .Returns((RelayClientError?)null);
        CreateSut();
        await ((IAsyncCommand)_sut.AddHubCommand).ExecuteAsync();
        var entry = _sut.Hubs.Single();
        entry.EditableName = "My Hub";
        entry.EditableBaseUrl = "http://my-hub.example";
        entry.EditableApiKey = "secret";

        // Act
        await ((IAsyncCommand)entry.SaveCommand).ExecuteAsync();

        // Assert
        await _hubConfigurationProvider.Received(1).AddHub(Arg.Any<HubConfigData>());
        await WaitFor(() => _relayRoomClient.ReceivedCalls()
            .Any(c => c.GetMethodInfo().Name == nameof(IRelayRoomClient.Health)));
        var persisted = _sut.Hubs.Single(h => h.Id == entry.Id);
        await WaitFor(() => persisted.Status == HubStatus.Online);
    }

    private static AssetProviderConfigData Provider(string id, AssetType assetType = AssetType.Units, bool isActive = true, bool isDefault = true) =>
        new(id, ProviderType.Bucket, assetType, "https://data/" + id, isActive, isDefault, 0);

    private void SetupAssetProviders(IReadOnlyList<AssetProviderConfigData> providers)
    {
        _assetProviderConfigurationProvider.GetProviders().Returns(Task.FromResult(providers));
    }

    [Fact]
    public async Task AttachHandlers_ShouldLoadAssetProviders()
    {
        // Arrange
        SetupAssetProviders([
            Provider("bucket"),
            Provider("local", isDefault: false)
        ]);
        CreateSut();

        // Act
        _sut.AttachHandlers();
        await WaitFor(() => _sut.AssetProviders.Count == 2);

        // Assert
        _sut.AssetProviders[0].Id.ShouldBe("bucket");
        _sut.AssetProviders[1].Id.ShouldBe("local");
        _sut.AssetProviders[1].CanRemove.ShouldBeTrue();
        _sut.AssetProviders[0].CanRemove.ShouldBeFalse();
    }

    [Fact]
    public async Task AttachHandlers_WhenOnlyActiveForAssetType_CanDeactivateIsFalse()
    {
        // Arrange
        SetupAssetProviders([
            new AssetProviderConfigData("a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: true, IsDefault: true, SortOrder: 0)
        ]);
        CreateSut();

        // Act
        _sut.AttachHandlers();
        await WaitFor(() => _sut.AssetProviders.Count == 1);

        // Assert
        _sut.AssetProviders.Single().CanDeactivate.ShouldBeFalse();
    }

    [Fact]
    public async Task AttachHandlers_WhenAnotherActiveOfSameAssetType_CanDeactivateIsTrue()
    {
        // Arrange
        SetupAssetProviders([
            new AssetProviderConfigData("a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: true, IsDefault: true, SortOrder: 0),
            new AssetProviderConfigData("b", ProviderType.Bucket, AssetType.Units, "u2", IsActive: true, IsDefault: true, SortOrder: 1)
        ]);
        CreateSut();

        // Act
        _sut.AttachHandlers();
        await WaitFor(() => _sut.AssetProviders.Count == 2);

        // Assert
        _sut.AssetProviders.ShouldAllBe(p => p.CanDeactivate);
    }

    [Fact]
    public async Task AttachHandlers_WhenInactiveProvider_CanDeactivateIsTrue()
    {
        // Arrange
        SetupAssetProviders([
            new AssetProviderConfigData("a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: false, IsDefault: true, SortOrder: 0)
        ]);
        CreateSut();

        // Act
        _sut.AttachHandlers();
        await WaitFor(() => _sut.AssetProviders.Count == 1);

        // Assert
        _sut.AssetProviders.Single().CanDeactivate.ShouldBeTrue();
    }

    [Fact]
    public async Task ToggleActiveCommand_WhenExecuted_PersistsActivationAndReloads()
    {
        // Arrange
        var providers = new List<AssetProviderConfigData>
        {
            new("a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: true, IsDefault: true, SortOrder: 0),
            new("b", ProviderType.Bucket, AssetType.Units, "u2", IsActive: true, IsDefault: true, SortOrder: 1)
        };
        _assetProviderConfigurationProvider.GetProviders()
            .Returns(_ => Task.FromResult<IReadOnlyList<AssetProviderConfigData>>(providers.ToArray()));
        CreateSut();
        _sut.AttachHandlers();
        await WaitFor(() => _sut.AssetProviders.Count == 2);
        var entry = _sut.AssetProviders.First(p => p.Id == "a");

        // Act
        await ((IAsyncCommand)entry.ToggleActiveCommand).ExecuteAsync();

        // Assert
        await _assetProviderConfigurationProvider.Received(1).SetProviderActive("a", false);
    }

    [Fact]
    public async Task RemoveAssetProviderCommand_WhenExecuted_RemovesProvider()
    {
        // Arrange
        var providers = new List<AssetProviderConfigData>
        {
            new("a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: true, IsDefault: true, SortOrder: 0),
            new("local", ProviderType.Filesystem, AssetType.Units, "u2", IsActive: true, IsDefault: false, SortOrder: 1)
        };
        _assetProviderConfigurationProvider.GetProviders()
            .Returns(_ => Task.FromResult<IReadOnlyList<AssetProviderConfigData>>(providers.ToArray()));
        CreateSut();
        _sut.AttachHandlers();
        await WaitFor(() => _sut.AssetProviders.Count == 2);
        var entry = _sut.AssetProviders.First(p => p.Id == "local");

        // Act
        await ((IAsyncCommand<AssetProviderEntryViewModel>)_sut.RemoveAssetProviderCommand).ExecuteAsync(entry);

        // Assert
        await _assetProviderConfigurationProvider.Received(1).RemoveProvider("local");
    }

    [Fact]
    public async Task RemoveAssetProviderCommand_WhenExecutedWithNull_DoesNotRemove()
    {
        // Arrange
        SetupAssetProviders([Provider("a")]);
        CreateSut();

        // Act
        await ((IAsyncCommand<AssetProviderEntryViewModel>)_sut.RemoveAssetProviderCommand).ExecuteAsync(null!);

        // Assert
        await _assetProviderConfigurationProvider.DidNotReceive().RemoveProvider(Arg.Any<string>());
    }

    [Fact]
    public async Task RemoveAssetProviderCommand_WhenDefault_DoesNotCallProvider()
    {
        // Arrange
        var providers = new List<AssetProviderConfigData>
        {
            new("a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: true, IsDefault: true, SortOrder: 0),
            new("b", ProviderType.Bucket, AssetType.Units, "u2", IsActive: true, IsDefault: true, SortOrder: 1)
        };
        _assetProviderConfigurationProvider.GetProviders()
            .Returns(_ => Task.FromResult<IReadOnlyList<AssetProviderConfigData>>(providers.ToArray()));
        CreateSut();
        _sut.AttachHandlers();
        await WaitFor(() => _sut.AssetProviders.Count == 2);
        var defaultEntry = _sut.AssetProviders.First(p => p.Id == "a");

        // Act
        await ((IAsyncCommand<AssetProviderEntryViewModel>)_sut.RemoveAssetProviderCommand).ExecuteAsync(defaultEntry);

        // Assert
        await _assetProviderConfigurationProvider.DidNotReceive().RemoveProvider(Arg.Any<string>());
    }

    [Fact]
    public async Task LoadAssetProvidersAsync_WhenGetProvidersThrows_ShouldLogError()
    {
        // Arrange
        _assetProviderConfigurationProvider.GetProviders()
            .Returns(Task.FromException<IReadOnlyList<AssetProviderConfigData>>(new Exception("providers unavailable")));
        CreateSut();

        // Act
        _sut.AttachHandlers();
        await WaitFor(() => _logger.ReceivedCalls().Any());

        // Assert
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to load asset providers")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()!);
    }

    [Fact]
    public async Task ToggleActiveCommand_WhenSetProviderActiveThrowsInvalidOperation_ShouldLogWarning()
    {
        // Arrange
        var providers = new List<AssetProviderConfigData>
        {
            new("a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: true, IsDefault: true, SortOrder: 0),
            new("b", ProviderType.Bucket, AssetType.Units, "u2", IsActive: true, IsDefault: true, SortOrder: 1)
        };
        _assetProviderConfigurationProvider.GetProviders()
            .Returns(_ => Task.FromResult<IReadOnlyList<AssetProviderConfigData>>(providers.ToArray()));
        _assetProviderConfigurationProvider.SetProviderActive("a", false)
            .ThrowsAsync(new InvalidOperationException("cannot deactivate"));
        CreateSut();
        _sut.AttachHandlers();
        await WaitFor(() => _sut.AssetProviders.Count == 2);
        var entry = _sut.AssetProviders.First(p => p.Id == "a");

        // Act
        await ((IAsyncCommand)entry.ToggleActiveCommand).ExecuteAsync();

        // Assert
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Cannot toggle provider")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()!);
    }

    [Fact]
    public async Task ToggleActiveCommand_WhenSetProviderActiveThrows_ShouldLogError()
    {
        // Arrange
        var providers = new List<AssetProviderConfigData>
        {
            new("a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: true, IsDefault: true, SortOrder: 0),
            new("b", ProviderType.Bucket, AssetType.Units, "u2", IsActive: true, IsDefault: true, SortOrder: 1)
        };
        _assetProviderConfigurationProvider.GetProviders()
            .Returns(_ => Task.FromResult<IReadOnlyList<AssetProviderConfigData>>(providers.ToArray()));
        _assetProviderConfigurationProvider.SetProviderActive("a", false)
            .ThrowsAsync(new Exception("unexpected failure"));
        CreateSut();
        _sut.AttachHandlers();
        await WaitFor(() => _sut.AssetProviders.Count == 2);
        var entry = _sut.AssetProviders.First(p => p.Id == "a");

        // Act
        await ((IAsyncCommand)entry.ToggleActiveCommand).ExecuteAsync();

        // Assert
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to toggle provider")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()!);
    }

    [Fact]
    public async Task RemoveAssetProviderCommand_WhenRemoveProviderThrowsInvalidOperation_ShouldLogWarning()
    {
        // Arrange
        var providers = new List<AssetProviderConfigData>
        {
            new("a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: true, IsDefault: true, SortOrder: 0),
            new("local", ProviderType.Filesystem, AssetType.Units, "u2", IsActive: true, IsDefault: false, SortOrder: 1)
        };
        _assetProviderConfigurationProvider.GetProviders()
            .Returns(_ => Task.FromResult<IReadOnlyList<AssetProviderConfigData>>(providers.ToArray()));
        _assetProviderConfigurationProvider.RemoveProvider("local")
            .ThrowsAsync(new InvalidOperationException("cannot remove"));
        CreateSut();
        _sut.AttachHandlers();
        await WaitFor(() => _sut.AssetProviders.Count == 2);
        var entry = _sut.AssetProviders.First(p => p.Id == "local");

        // Act
        await ((IAsyncCommand<AssetProviderEntryViewModel>)_sut.RemoveAssetProviderCommand).ExecuteAsync(entry);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Cannot remove provider")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()!);
    }

    [Fact]
    public async Task RemoveAssetProviderCommand_WhenRemoveProviderThrows_ShouldLogError()
    {
        // Arrange
        var providers = new List<AssetProviderConfigData>
        {
            new("a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: true, IsDefault: true, SortOrder: 0),
            new("local", ProviderType.Filesystem, AssetType.Units, "u2", IsActive: true, IsDefault: false, SortOrder: 1)
        };
        _assetProviderConfigurationProvider.GetProviders()
            .Returns(_ => Task.FromResult<IReadOnlyList<AssetProviderConfigData>>(providers.ToArray()));
        _assetProviderConfigurationProvider.RemoveProvider("local")
            .ThrowsAsync(new Exception("unexpected failure"));
        CreateSut();
        _sut.AttachHandlers();
        await WaitFor(() => _sut.AssetProviders.Count == 2);
        var entry = _sut.AssetProviders.First(p => p.Id == "local");

        // Act
        await ((IAsyncCommand<AssetProviderEntryViewModel>)_sut.RemoveAssetProviderCommand).ExecuteAsync(entry);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to remove provider")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()!);
    }

    private void SetupGrowingAssetProviders(List<AssetProviderConfigData> providers)
    {
        _assetProviderConfigurationProvider.GetProviders()
            .Returns(_ => Task.FromResult<IReadOnlyList<AssetProviderConfigData>>(providers.ToArray()));
        _assetProviderConfigurationProvider.AddProvider(Arg.Any<AssetProviderConfigData>())
            .Returns(Task.CompletedTask)
            .AndDoes(callInfo => providers.Add(callInfo.Arg<AssetProviderConfigData>()));
    }

    [Fact]
    public async Task AddProviderCommand_WhenExecuted_ShouldAddProviderAndShowInCollection()
    {
        // Arrange
        var providers = new List<AssetProviderConfigData>();
        SetupGrowingAssetProviders(providers);
        CreateSut();
        _sut.SelectedAddProviderType = ProviderType.Filesystem;
        _sut.SelectedAddAssetType = AssetType.Hexes;
        _sut.AddProviderUrlOrPath = "/data/hexes";

        // Act
        await ((IAsyncCommand)_sut.AddProviderCommand).ExecuteAsync();
        await WaitFor(() => _sut.AssetProviders.Count == 1);

        // Assert
        await _assetProviderConfigurationProvider.Received(1).AddProvider(Arg.Is<AssetProviderConfigData>(p =>
            p.ProviderType == ProviderType.Filesystem &&
            p.AssetType == AssetType.Hexes &&
            p.UrlOrPath == "/data/hexes" &&
            p.IsActive &&
            !p.IsDefault));
        _sut.AssetProviders.Single().AssetType.ShouldBe(AssetType.Hexes);
        _sut.AddProviderUrlOrPath.ShouldBeEmpty();
        _sut.AddProviderValidationMessage.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddProviderCommand_WhenUrlOrPathEmpty_ShouldShowValidationAndNotAdd()
    {
        // Arrange
        var providers = new List<AssetProviderConfigData>();
        SetupGrowingAssetProviders(providers);
        CreateSut();
        _sut.AddProviderUrlOrPath = "   ";

        // Act
        await ((IAsyncCommand)_sut.AddProviderCommand).ExecuteAsync();

        // Assert
        await _assetProviderConfigurationProvider.DidNotReceive().AddProvider(Arg.Any<AssetProviderConfigData>());
        _sut.AssetProviders.ShouldBeEmpty();
        _sut.AddProviderValidationMessage.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AddProviderCommand_WhenAddProviderThrows_ShouldLogError()
    {
        // Arrange
        _assetProviderConfigurationProvider.GetProviders()
            .Returns(Task.FromResult<IReadOnlyList<AssetProviderConfigData>>([]));
        _assetProviderConfigurationProvider.AddProvider(Arg.Any<AssetProviderConfigData>())
            .ThrowsAsync(new Exception("persist failed"));
        CreateSut();
        _sut.AddProviderUrlOrPath = "/data/units";

        // Act
        await ((IAsyncCommand)_sut.AddProviderCommand).ExecuteAsync();

        // Assert
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to add asset provider")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()!);
    }

    [Fact]
    public async Task ReloadProvidersCommand_WhenExecuted_ShouldTriggerAssetReloadAndRefreshCacheStatus()
    {
        // Arrange
        SetupAssetProviders([Provider("a")]);
        _unitCachingService.GetAvailableModels().Returns([]);
        _terrainAssetService.GetLoadedBiomes().Returns([]);
        CreateSut();
        _sut.AttachHandlers();
        await WaitFor(() => _sut.AssetProviders.Count == 1);

        // Act
        await ((IAsyncCommand)_sut.ReloadProvidersCommand).ExecuteAsync();

        // Assert
        await _unitCachingService.Received(1).ClearCache();
        await _terrainAssetService.Received(1).ClearCache();
        _sut.CacheStatus.ShouldBe("Loaded units: 0, Loaded biomes: 0");
        _sut.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task ReloadProvidersCommand_WhenReloadThrows_ShouldLogErrorAndResetIsBusy()
    {
        // Arrange
        SetupAssetProviders([Provider("a")]);
        _unitCachingService.ClearCache().Returns(Task.FromException(new Exception("reload failed")));
        CreateSut();
        _sut.AttachHandlers();
        await WaitFor(() => _sut.AssetProviders.Count == 1);

        // Act
        await ((IAsyncCommand)_sut.ReloadProvidersCommand).ExecuteAsync();

        // Assert
        _sut.IsBusy.ShouldBeFalse();
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to reload asset providers")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()!);
    }

    [Fact]
    public void ProviderTypes_ShouldExposeAllProviderTypes()
    {
        // Arrange
        CreateSut();

        // Assert
        _sut.ProviderTypes.ShouldBe([ProviderType.Bucket, ProviderType.GitHub, ProviderType.Filesystem]);
        _sut.AssetTypes.ShouldBe([AssetType.Units, AssetType.Hexes]);
    }
}
