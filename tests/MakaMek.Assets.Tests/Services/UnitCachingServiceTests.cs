using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Core.Data.Serialization.Converters;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Assets.ResourceProviders;

namespace Sanet.MakaMek.Assets.Tests.Services;

public class UnitCachingServiceTests
{
    private readonly IResourceStreamProvider _resourceProvider = Substitute.For<IResourceStreamProvider>();
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();
    private readonly ILogger<UnitCachingService> _logger = Substitute.For<ILogger<UnitCachingService>>();
    
    public UnitCachingServiceTests()
    {
        _loggerFactory.CreateLogger<UnitCachingService>().Returns(_logger);
    }
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters =
        {
            new EnumConverter<MakaMekComponent>(),
            new EnumConverter<PartLocation>(),
            new EnumConverter<MovementType>(),
            new EnumConverter<UnitStatus>(),
            new EnumConverter<WeightClass>()
        }
    };
    private UnitCachingService CreateServiceWithMockProvider(string unitId, Stream mmuxStream)
    {
        _resourceProvider.GetAvailableResourceIds().Returns([unitId]);
        _resourceProvider.GetResourceStream(unitId).Returns(mmuxStream);
        _resourceProvider.ClearReceivedCalls();

        return new UnitCachingService([_resourceProvider], _loggerFactory);
    }

    private static Stream CreateTestMmuxStream(string model, string chassis)
    {
        var memoryStream = new MemoryStream();
        
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // Create unit.json
            var unitData = new UnitData
            {
                Model = model,
                Chassis = chassis,
                Mass = 20,
                EngineRating = 160,
                EngineType = "Standard",
                ArmorValues = new Dictionary<PartLocation, ArmorLocation>(),
                Equipment = new List<ComponentData>(),
                AdditionalAttributes = new Dictionary<string, string>(),
                Quirks = new Dictionary<string, string>()
            };

            var unitJsonEntry = archive.CreateEntry("unit.json");
            using (var entryStream = unitJsonEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var json = JsonSerializer.Serialize(unitData, JsonOptions);
                writer.Write(json);
            }

            // Create unit.png (minimal PNG data)
            var unitImageEntry = archive.CreateEntry("unit.png");
            using (var entryStream = unitImageEntry.Open())
            {
                // Write a minimal PNG header (not a valid image, but sufficient for testing)
                var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
                entryStream.Write(pngHeader, 0, pngHeader.Length);
            }
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    private static Stream CreateMmuxStreamMissingUnitJson()
    {
        var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // Only create unit.png, no unit.json
            var unitImageEntry = archive.CreateEntry("unit.png");
            using (var entryStream = unitImageEntry.Open())
            {
                var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
                entryStream.Write(pngHeader, 0, pngHeader.Length);
            }
        }
        memoryStream.Position = 0;
        return memoryStream;
    }

    private static Stream CreateMmuxStreamMissingImage(string model, string chassis)
    {
        var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // Create unit.json only
            var unitData = new UnitData
            {
                Model = model,
                Chassis = chassis,
                Mass = 20,
                EngineRating = 160,
                EngineType = "Standard",
                ArmorValues = new Dictionary<PartLocation, ArmorLocation>(),
                Equipment = new List<ComponentData>(),
                AdditionalAttributes = new Dictionary<string, string>(),
                Quirks = new Dictionary<string, string>()
            };

            var unitJsonEntry = archive.CreateEntry("unit.json");
            using (var entryStream = unitJsonEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var json = JsonSerializer.Serialize(unitData, JsonOptions);
                writer.Write(json);
            }
        }
        memoryStream.Position = 0;
        return memoryStream;
    }

    private static Stream CreateMmuxStreamWithInvalidUnitJson()
    {
        var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // Create unit.json with a missing Model property (deserializes to UnitData with null Model)
            var unitData = new UnitData
            {
                Model = "",
                Chassis = "Test",
                Mass = 20,
                EngineRating = 160,
                EngineType = "Standard",
                ArmorValues = new Dictionary<PartLocation, ArmorLocation>(),
                Equipment = new List<ComponentData>(),
                AdditionalAttributes = new Dictionary<string, string>(),
                Quirks = new Dictionary<string, string>()
            };
            var unitJsonEntry = archive.CreateEntry("unit.json");
            using (var entryStream = unitJsonEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                writer.Write(JsonSerializer.Serialize(unitData, JsonOptions));
            }
        }
        memoryStream.Position = 0;
        return memoryStream;
    }

    [Fact]
    public async Task GetAvailableModels_ShouldReturnModels_WhenInitialized()
    {
        // Arrange
        await using var mmuxStream = CreateTestMmuxStream("LCT-1V", "Locust");
        var sut = CreateServiceWithMockProvider("LCT-1V", mmuxStream);

        // Act
        var models =(await sut.GetAvailableModels()).ToList();

        // Assert
        models.ShouldNotBeNull();
        models.ShouldContain("LCT-1V");
    }

    [Fact]
    public async Task GetUnitData_ShouldReturnUnitData_WhenModelExists()
    {
        // Arrange
        await using var mmuxStream = CreateTestMmuxStream("LCT-1V", "Locust");
        var sut = CreateServiceWithMockProvider("LCT-1V", mmuxStream);

        // Act
        var unitData = await sut.GetUnitData("LCT-1V");

        // Assert
        unitData.ShouldNotBeNull();
        unitData.Value.Model.ShouldBe("LCT-1V");
        unitData.Value.Chassis.ShouldBe("Locust");
    }

    [Fact]
    public async Task GetUnitImage_ShouldReturnImageBytes_WhenModelExists()
    {
        // Arrange
        await using var mmuxStream = CreateTestMmuxStream("LCT-1V", "Locust");
        var sut = CreateServiceWithMockProvider("LCT-1V", mmuxStream);

        // Act
        var imageBytes = await sut.GetUnitImage("LCT-1V");

        // Assert
        imageBytes.ShouldNotBeNull();
        imageBytes.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetAllUnits_ShouldReturnAllUnits_WhenInitialized()
    {
        // Arrange
        await using var mmuxStream = CreateTestMmuxStream("LCT-1V", "Locust");
        var sut = CreateServiceWithMockProvider("LCT-1V", mmuxStream);

        // Act
        var units = (await sut.GetAllUnits()).ToList();

        // Assert
        units.ShouldNotBeEmpty();
        units.ShouldContain(u => u.Model == "LCT-1V");
    }

    [Fact]
    public async Task ClearCache_ShouldForceReinitialization()
    {
        // Arrange
        await using var mmuxStream1 = CreateTestMmuxStream("LCT-1V", "Locust");
        await using var mmuxStream2 = CreateTestMmuxStream("LCT-1V", "Locust");
        var sut = CreateServiceWithMockProvider("LCT-1V", mmuxStream1);
        // Return a fresh stream for the second initialization after ClearCache
        _resourceProvider.GetResourceStream("LCT-1V").Returns(mmuxStream1, mmuxStream2);
        
        // Ensure the cache is initialized
        var initialModels = (await sut.GetAvailableModels()).ToList();
        initialModels.ShouldNotBeEmpty();

        // Act
        await sut.ClearCache();
        var modelsAfterClear = (await sut.GetAvailableModels()).ToList();

        // Assert
        modelsAfterClear.ShouldNotBeEmpty();
        await _resourceProvider.Received(2).GetAvailableResourceIds();
    }
    
    [Fact]
    public async Task GetAvailableModels_ShouldReturnedCachedData_OnSecondInvocation()
    {
        // Arrange
        await using var mmuxStream = CreateTestMmuxStream("LCT-1V", "Locust");
        _resourceProvider.GetResourceStream("LCT-1V").Returns(mmuxStream);
        var sut = CreateServiceWithMockProvider("LCT-1V", mmuxStream);
        
        // Ensure the cache is initialized
        var initialModels = (await sut.GetAvailableModels()).ToList();
        initialModels.ShouldNotBeEmpty();

        // Act
        var modelsAfterClear = (await sut.GetAvailableModels()).ToList();

        // Assert
        modelsAfterClear.ShouldNotBeEmpty();
        await _resourceProvider.Received(1).GetAvailableResourceIds();
    }

    [Fact]
    public async Task Service_ShouldHandleMultipleProviders()
    {
        // Arrange
        var mockProvider1 = Substitute.For<IResourceStreamProvider>();
        mockProvider1.GetAvailableResourceIds().Returns(["LCT-1V"]);
        await using var mmuxStream1 = CreateTestMmuxStream("LCT-1V", "Locust");
        mockProvider1.GetResourceStream("LCT-1V").Returns(mmuxStream1);

        var mockProvider2 = Substitute.For<IResourceStreamProvider>();
        mockProvider2.GetAvailableResourceIds().Returns(["SHD-2D"]);
        await using var mmuxStream2 = CreateTestMmuxStream("SHD-2D", "Shadowhawk");
        mockProvider2.GetResourceStream("SHD-2D").Returns(mmuxStream2);

        var sut = new UnitCachingService([mockProvider1, mockProvider2], _loggerFactory);

        // Act
        var models = (await sut.GetAvailableModels()).ToList();

        // Assert
        models.ShouldContain("LCT-1V");
        models.ShouldContain("SHD-2D");
        models.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Service_ShouldHandleEmptyProviders()
    {
        // Arrange
        var sut = new UnitCachingService([], _loggerFactory);

        // Act
        var models = await sut.GetAvailableModels();

        // Assert
        models.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadUnitsFromStreamProviders_ShouldContinueOnResourceAndProviderErrors()
    {
        // Arrange
        var mockProvider1 = Substitute.For<IResourceStreamProvider>();
        // Provider1 returns two IDs: one good and one bad that will throw when fetching a stream
        mockProvider1.GetAvailableResourceIds().Returns(["GOOD", "BAD"]);
        await using var goodStream = CreateTestMmuxStream("LCT-1V", "Locust");
        mockProvider1.GetResourceStream("GOOD").Returns(goodStream);
        mockProvider1.GetResourceStream("BAD")
            .Returns(Task.FromException<Stream?>(new Exception("bad resource error")));

        var mockProvider2 = Substitute.For<IResourceStreamProvider>();
        // Provider2 will throw when listing resources to trigger provider-level catch
        mockProvider2.GetAvailableResourceIds()
            .Returns(Task.FromException<IEnumerable<string>>(new Exception("provider enumeration failed")));

        var sut = new UnitCachingService([mockProvider1, mockProvider2], _loggerFactory);

        // Act
        var models = (await sut.GetAvailableModels()).ToList();

        // Assert: a valid model from GOOD should be present; BAD should not stop processing
        models.ShouldContain("LCT-1V");
        models.ShouldNotContain("BAD");

        // Verify that LogError was called
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception>(ex => ex.Message == "bad resource error"),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task LoadUnitsFromStreamProviders_ShouldLogAndSkip_WhenUnitJsonMissing()
    {
        // Arrange
        var mockProvider = Substitute.For<IResourceStreamProvider>();
        mockProvider.GetAvailableResourceIds().Returns(["MISSING_JSON"]);
        await using var badStream = CreateMmuxStreamMissingUnitJson();
        mockProvider.GetResourceStream("MISSING_JSON").Returns(badStream);

        var sut = new UnitCachingService([mockProvider], _loggerFactory);

        // Act
        var models = (await sut.GetAvailableModels()).ToList();

        // Assert: no models added
        models.ShouldBeEmpty();

        // Verify that LogError was called
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception>(ex => ex.Message == "MMUX package missing unit.json"),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task LoadUnitsFromStreamProviders_ShouldLogAndSkip_WhenUnitPngMissing()
    {
        // Arrange
        var mockProvider = Substitute.For<IResourceStreamProvider>();
        mockProvider.GetAvailableResourceIds().Returns(["MISSING_PNG"]);
        await using var badStream = CreateMmuxStreamMissingImage("ABC-1", "Test");
        mockProvider.GetResourceStream("MISSING_PNG").Returns(badStream);

        var sut = new UnitCachingService([mockProvider], _loggerFactory);

        // Act
        var models = (await sut.GetAvailableModels()).ToList();

        // Assert: model should not be added because image missing causes an exception
        models.ShouldBeEmpty();

        // Verify that LogError was called
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception>(ex => ex.Message == "MMUX package missing unit.png"),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task LoadUnitsFromStreamProviders_ShouldLogAndSkip_WhenUnitJsonInvalidModel()
    {
        // Arrange
        var mockProvider = Substitute.For<IResourceStreamProvider>();
        mockProvider.GetAvailableResourceIds().Returns(["INVALID_UNIT_JSON"]);
        await using var badStream = CreateMmuxStreamWithInvalidUnitJson();
        mockProvider.GetResourceStream("INVALID_UNIT_JSON").Returns(badStream);

        var sut = new UnitCachingService([mockProvider], _loggerFactory);

        // Act
        var models = (await sut.GetAvailableModels()).ToList();

        // Assert: no models added due to invalid unit.json (missing/empty Model)
        models.ShouldBeEmpty();

        // Verify that LogError was called
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception>(ex => ex.Message == "Failed to deserialize unit.json"),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task LoadUnits_ShouldRaiseLoadProgressEvents_WithIncreasingCounts()
    {
        // Arrange
        const int unitCount = 5;
        var unitIds = Enumerable.Range(1, unitCount).Select(i => $"UNIT-{i}").ToArray();
        var provider = Substitute.For<IResourceStreamProvider>();
        provider.GetAvailableResourceIds().Returns(unitIds);
        provider.GetResourceStream(Arg.Any<string>())
            .Returns(ci => CreateTestMmuxStream((string)ci[0], "Test"));

        var sut = new UnitCachingService([provider], _loggerFactory);
        var progressEvents = new List<ResourceLoadProgressEventArgs>();
        sut.LoadProgress += (_, e) => progressEvents.Add(e);

        // Act
        var models = (await sut.GetAvailableModels()).ToList();

        // Assert
        progressEvents.ShouldNotBeEmpty();
        progressEvents[0].LoadedCount.ShouldBe(0);
        progressEvents[0].TotalCount.ShouldBe(unitCount);
        progressEvents[^1].LoadedCount.ShouldBe(unitCount);
        progressEvents[^1].TotalCount.ShouldBe(unitCount);
        progressEvents.Select(e => e.LoadedCount).ShouldBeInOrder();
        models.Count.ShouldBe(unitCount);
    }

    [Fact]
    public async Task LoadUnits_ShouldKeepTotalCountStable_AcrossMultipleProviders()
    {
        // Arrange
        var provider1 = Substitute.For<IResourceStreamProvider>();
        provider1.GetAvailableResourceIds().Returns(["LCT-1V"]);
        provider1.GetResourceStream("LCT-1V").Returns(CreateTestMmuxStream("LCT-1V", "Locust"));

        var provider2 = Substitute.For<IResourceStreamProvider>();
        provider2.GetAvailableResourceIds().Returns(["SHD-2D", "WVR-6R"]);
        provider2.GetResourceStream(Arg.Any<string>())
            .Returns(ci => CreateTestMmuxStream((string)ci[0], "Test"));

        var sut = new UnitCachingService([provider1, provider2], _loggerFactory);
        var progressEvents = new List<ResourceLoadProgressEventArgs>();
        sut.LoadProgress += (_, e) => progressEvents.Add(e);

        // Act
        var models = (await sut.GetAvailableModels()).ToList();

        // Assert
        models.Count.ShouldBe(3);
        progressEvents.ShouldNotBeEmpty();
        // The total is finalized up front (3) and never changes while loading
        progressEvents.Select(e => e.TotalCount).Distinct().ShouldHaveSingleItem();
        progressEvents[0].TotalCount.ShouldBe(3);
        progressEvents[^1].LoadedCount.ShouldBe(3);
        // Reported progress (loaded/total) never decreases
        var normalized = progressEvents.Select(e => (double)e.LoadedCount / e.TotalCount).ToList();
        for (var i = 1; i < normalized.Count; i++)
            normalized[i].ShouldBeGreaterThanOrEqualTo(normalized[i - 1]);
    }

    [Fact]
    public async Task LoadUnits_ShouldAdvanceProgress_WhenLaterTaskCompletesBeforeDelayedEarlierTask()
    {
        // Arrange
        var provider = Substitute.For<IResourceStreamProvider>();
        // DELAYED comes first in the batch but is blocked; FAST completes first
        provider.GetAvailableResourceIds().Returns(["DELAYED", "FAST"]);

        var delayedGate = new TaskCompletionSource<Stream?>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.GetResourceStream("DELAYED").Returns(delayedGate.Task);
        provider.GetResourceStream("FAST").Returns(CreateTestMmuxStream("FAST", "Fast"));

        var sut = new UnitCachingService([provider], _loggerFactory);
        var progressEvents = new List<ResourceLoadProgressEventArgs>();
        sut.LoadProgress += (_, e) => progressEvents.Add(e);

        // Act - start loading; FAST completes while DELAYED is still blocked
        var loadTask = sut.GetAvailableModels();

        // Progress must advance past zero as soon as FAST completes, without waiting on DELAYED
        var advancedBeforeRelease = await Task.Run(async () =>
        {
            for (var i = 0; i < 100; i++)
            {
                if (progressEvents.Any(e => e.LoadedCount > 0)) return true;
                await Task.Delay(10);
            }
            return false;
        });

        // Assert - progress reports 1/2 (FAST done) while DELAYED is still pending
        advancedBeforeRelease.ShouldBeTrue();
        progressEvents.Any(e => e.LoadedCount == 1 && e.TotalCount == 2).ShouldBeTrue();

        // Release the delayed task and finish
        delayedGate.SetResult(CreateTestMmuxStream("DELAYED", "Delayed"));
        await loadTask;

        progressEvents[^1].LoadedCount.ShouldBe(2);
        progressEvents[^1].TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task SetProviders_ShouldReplaceProviderSet_AndForceReinitialization()
    {
        // Arrange
        await using var streamA = CreateTestMmuxStream("LCT-1V", "Locust");
        var providerA = Substitute.For<IResourceStreamProvider>();
        providerA.GetAvailableResourceIds().Returns(["LCT-1V"]);
        providerA.GetResourceStream("LCT-1V").Returns(streamA);

        await using var streamB = CreateTestMmuxStream("SHD-2D", "Shadowhawk");
        var providerB = Substitute.For<IResourceStreamProvider>();
        providerB.GetAvailableResourceIds().Returns(["SHD-2D"]);
        providerB.GetResourceStream("SHD-2D").Returns(streamB);

        var sut = new UnitCachingService([providerA], _loggerFactory);

        // Act
        var modelsBefore = (await sut.GetAvailableModels()).ToList();
        await sut.SetProviders([providerB]);
        var modelsAfter = (await sut.GetAvailableModels()).ToList();

        // Assert
        modelsBefore.ShouldContain("LCT-1V");
        modelsAfter.ShouldContain("SHD-2D");
        modelsAfter.ShouldNotContain("LCT-1V");
    }

    [Fact]
    public async Task LoadUnits_RepeatedProviderPositions_AreProcessedSeparately_WithFinalOccurrenceAuthoritative()
    {
        // Arrange - the same provider instance appears at positions 0 and 2, with B between
        // them. All three produce the same model; A's second (position 2) occurrence must be
        // authoritative even though B is a different provider in between.
        var providerA = Substitute.For<IResourceStreamProvider>();
        providerA.GetAvailableResourceIds().Returns(["LCT-1V"]);
        providerA.GetResourceStream("LCT-1V")
            .Returns(CreateTestMmuxStream("LCT-1V", "Locust-A"),
                     CreateTestMmuxStream("LCT-1V", "Locust-A-Last"));

        var providerB = Substitute.For<IResourceStreamProvider>();
        providerB.GetAvailableResourceIds().Returns(["LCT-1V"]);
        providerB.GetResourceStream("LCT-1V").Returns(CreateTestMmuxStream("LCT-1V", "Locust-B"));

        var sut = new UnitCachingService([providerA, providerB, providerA], _loggerFactory);

        // Act
        var unitData = await sut.GetUnitData("LCT-1V");

        // Assert - position 2 (the final 'A') loads last and therefore overwrites both the
        // position 0 'A' and the middle 'B'.
        unitData.ShouldNotBeNull();
        unitData.Value.Chassis.ShouldBe("Locust-A-Last");
    }

    [Fact]
    public async Task Service_LaterProviderShouldOverwriteEarlier_WhenSameModel()
    {
        // Arrange — provider1 serves "LCT-1V" with chassis "Locust-1";
        // provider2 (lower in list) serves the same model with chassis "Locust-2".
        // The later provider must win.
        var provider1 = Substitute.For<IResourceStreamProvider>();
        provider1.GetAvailableResourceIds().Returns(["LCT-1V"]);
        provider1.GetResourceStream("LCT-1V")
            .Returns(CreateTestMmuxStream("LCT-1V", "Locust-1"));

        var provider2 = Substitute.For<IResourceStreamProvider>();
        provider2.GetAvailableResourceIds().Returns(["LCT-1V"]);
        provider2.GetResourceStream("LCT-1V")
            .Returns(CreateTestMmuxStream("LCT-1V", "Locust-2"));

        var sut = new UnitCachingService([provider1, provider2], _loggerFactory);

        // Act
        var models = (await sut.GetAvailableModels()).ToList();
        var unitData = await sut.GetUnitData("LCT-1V");

        // Assert — only one unit, chassis reflects provider2 (later/lower in list)
        models.Count.ShouldBe(1);
        models.ShouldContain("LCT-1V");
        unitData.ShouldNotBeNull();
        unitData.Value.Chassis.ShouldBe("Locust-2");
    }

    [Fact]
    public async Task Service_UniqueAssetsFromBothProviders_ShouldAllBePresent()
    {
        // Arrange — provider1 serves LCT-1V, provider2 serves SHD-2D and WVR-6R
        var provider1 = Substitute.For<IResourceStreamProvider>();
        provider1.GetAvailableResourceIds().Returns(["LCT-1V"]);
        provider1.GetResourceStream("LCT-1V")
            .Returns(CreateTestMmuxStream("LCT-1V", "Locust"));

        var provider2 = Substitute.For<IResourceStreamProvider>();
        provider2.GetAvailableResourceIds().Returns(["SHD-2D", "WVR-6R"]);
        provider2.GetResourceStream(Arg.Any<string>())
            .Returns(ci => CreateTestMmuxStream((string)ci[0], "Test"));

        var sut = new UnitCachingService([provider1, provider2], _loggerFactory);

        // Act
        var models = (await sut.GetAvailableModels()).ToList();

        // Assert — all three unique units from both providers are present
        models.Count.ShouldBe(3);
        models.ShouldContain("LCT-1V");
        models.ShouldContain("SHD-2D");
        models.ShouldContain("WVR-6R");
    }

    [Fact]
    public async Task ReloadProviders_ShouldReinitialize_FromCurrentProviders()
    {
        // Arrange
        await using var stream1 = CreateTestMmuxStream("LCT-1V", "Locust");
        await using var stream2 = CreateTestMmuxStream("LCT-1V", "Locust");
        var provider = Substitute.For<IResourceStreamProvider>();
        provider.GetAvailableResourceIds().Returns(["LCT-1V"]);
        provider.GetResourceStream("LCT-1V").Returns(stream1, stream2);

        var sut = new UnitCachingService([provider], _loggerFactory);
        await sut.GetAvailableModels();

        // Act
        await sut.ReloadProviders();

        // Assert - reload re-enumerates the provider
        await provider.Received(2).GetAvailableResourceIds();
    }

    [Fact]
    public async Task ConcurrentReloadAndAccess_ShouldNotCorruptState()
    {
        // Arrange
        const int unitCount = 5;
        var provider = Substitute.For<IResourceStreamProvider>();
        provider.GetAvailableResourceIds().Returns(
            Enumerable.Range(1, unitCount).Select(i => $"UNIT-{i}").ToArray());
        provider.GetResourceStream(Arg.Any<string>())
            .Returns(ci => CreateTestMmuxStream((string)ci[0], "Test"));

        var sut = new UnitCachingService([provider], _loggerFactory);

        // Act - hammer reloads and reads concurrently
        var reloadTask = Task.Run(async () =>
        {
            for (var i = 0; i < 25; i++)
            {
                await sut.ReloadProviders();
            }
        });

        var readTask = Task.Run(async () =>
        {
            for (var i = 0; i < 25; i++)
            {
                await sut.GetAvailableModels();
            }
        });

        await Task.WhenAll(reloadTask, readTask);

        // Assert - state is consistent after concurrent operations
        var models = (await sut.GetAvailableModels()).ToList();
        models.Count.ShouldBe(unitCount);
    }
}
