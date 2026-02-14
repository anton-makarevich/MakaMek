using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Serialization.Converters;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services;
using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Tests.Services;

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
        sut.ClearCache();
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
}
