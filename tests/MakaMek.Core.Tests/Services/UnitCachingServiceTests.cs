using System.IO.Compression;
using System.Text.Json;
using Sanet.MakaMek.Core.Data.Serialization.Converters;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services;
using Shouldly;
using NSubstitute;

namespace Sanet.MakaMek.Core.Tests.Services;

public class UnitCachingServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
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
    private static UnitCachingService CreateServiceWithMockProvider(string unitId, Stream mmuxStream)
    {
        var mockProvider = Substitute.For<IResourceStreamProvider>();
        mockProvider.GetAvailableResourceIds().Returns([unitId]);
        mockProvider.GetResourceStream(unitId).Returns(mmuxStream);

        return new UnitCachingService([mockProvider]);
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
                WalkMp = 8,
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
                WalkMp = 8,
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
    public async Task ClearCache_ShouldClearAllData()
    {
        // Arrange
        await using var mmuxStream = CreateTestMmuxStream("LCT-1V", "Locust");
        var sut = CreateServiceWithMockProvider("LCT-1V", mmuxStream);
        
        // Ensure the cache is initialized
        var initialModels = (await sut.GetAvailableModels()).ToList();
        initialModels.ShouldNotBeEmpty();

        // Act
        sut.ClearCache();
        var modelsAfterClear = (await sut.GetAvailableModels()).ToList();

        // Assert
        modelsAfterClear.ShouldBeEmpty();
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

        var sut = new UnitCachingService([mockProvider1, mockProvider2]);

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
        var sut = new UnitCachingService([]);

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
        mockProvider1
            .When(x => x.GetResourceStream("BAD"))
            .Do(_ => throw new Exception("bad resource error"));

        var mockProvider2 = Substitute.For<IResourceStreamProvider>();
        // Provider2 will throw when listing resources to trigger provider-level catch
        mockProvider2
            .When(x => x.GetAvailableResourceIds())
            .Do(_ => throw new Exception("provider enumeration failed"));

        var sut = new UnitCachingService([mockProvider1, mockProvider2]);

        // Capture console output to verify logging
        var originalOut = Console.Out;
        await using var consoleCapture = new StringWriter();
        Console.SetOut(consoleCapture);
        try
        {
            // Act
            var models = (await sut.GetAvailableModels()).ToList();

            // Assert: valid model from GOOD should be present; BAD should not stop processing
            models.ShouldContain("LCT-1V");
            models.ShouldNotContain("BAD");

            var log = consoleCapture.ToString();
            // Error from resource-level catch (line 150)
            log.ShouldContain("Error loading unit 'BAD' from provider");
            // Error from provider-level catch (line 157)
            log.ShouldContain("Error loading units from provider");
        }
        finally
        {
            // Restore console
            Console.SetOut(originalOut);
        }
    }
    
    [Fact]
    public async Task LoadUnitsFromStreamProviders_ShouldLogAndSkip_WhenUnitJsonMissing()
    {
        // Arrange
        var mockProvider = Substitute.For<IResourceStreamProvider>();
        mockProvider.GetAvailableResourceIds().Returns(["MISSING_JSON"]);
        await using var badStream = CreateMmuxStreamMissingUnitJson();
        mockProvider.GetResourceStream("MISSING_JSON").Returns(badStream);

        var sut = new UnitCachingService([mockProvider]);

        var originalOut = Console.Out;
        await using var consoleCapture = new StringWriter();
        Console.SetOut(consoleCapture);
        try
        {
            // Act
            var models = (await sut.GetAvailableModels()).ToList();

            // Assert: no models added
            models.ShouldBeEmpty();

            var log = consoleCapture.ToString();
            log.ShouldContain("Error loading unit 'MISSING_JSON' from provider");
            log.ShouldContain("missing unit.json");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task LoadUnitsFromStreamProviders_ShouldLogAndSkip_WhenUnitPngMissing()
    {
        // Arrange
        var mockProvider = Substitute.For<IResourceStreamProvider>();
        mockProvider.GetAvailableResourceIds().Returns(["MISSING_PNG"]);
        await using var badStream = CreateMmuxStreamMissingImage("ABC-1", "Test");
        mockProvider.GetResourceStream("MISSING_PNG").Returns(badStream);

        var sut = new UnitCachingService([mockProvider]);

        var originalOut = Console.Out;
        await using var consoleCapture = new StringWriter();
        Console.SetOut(consoleCapture);
        try
        {
            // Act
            var models = (await sut.GetAvailableModels()).ToList();

            // Assert: model should not be added because image missing causes an exception
            models.ShouldBeEmpty();

            var log = consoleCapture.ToString();
            log.ShouldContain("Error loading unit 'MISSING_PNG' from provider");
            log.ShouldContain("missing unit.png");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
