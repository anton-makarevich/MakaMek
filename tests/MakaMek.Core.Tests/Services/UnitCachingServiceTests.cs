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

    [Fact]
    public async Task GetAvailableModels_ShouldReturnModels_WhenInitialized()
    {
        // Arrange
        await using var mmuxStream = CreateTestMmuxStream("LCT-1V", "Locust");
        var service = CreateServiceWithMockProvider("LCT-1V", mmuxStream);

        // Act
        var models =(await service.GetAvailableModels()).ToList();

        // Assert
        models.ShouldNotBeNull();
        models.ShouldContain("LCT-1V");
    }

    [Fact]
    public async Task GetUnitData_ShouldReturnUnitData_WhenModelExists()
    {
        // Arrange
        await using var mmuxStream = CreateTestMmuxStream("LCT-1V", "Locust");
        var service = CreateServiceWithMockProvider("LCT-1V", mmuxStream);

        // Act
        var unitData = await service.GetUnitData("LCT-1V");

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
        var service = CreateServiceWithMockProvider("LCT-1V", mmuxStream);

        // Act
        var imageBytes = await service.GetUnitImage("LCT-1V");

        // Assert
        imageBytes.ShouldNotBeNull();
        imageBytes.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetAllUnits_ShouldReturnAllUnits_WhenInitialized()
    {
        // Arrange
        await using var mmuxStream = CreateTestMmuxStream("LCT-1V", "Locust");
        var service = CreateServiceWithMockProvider("LCT-1V", mmuxStream);

        // Act
        var units = (await service.GetAllUnits()).ToList();

        // Assert
        units.ShouldNotBeEmpty();
        units.ShouldContain(u => u.Model == "LCT-1V");
    }

    [Fact]
    public async Task ClearCache_ShouldClearAllData()
    {
        // Arrange
        await using var mmuxStream = CreateTestMmuxStream("LCT-1V", "Locust");
        var service = CreateServiceWithMockProvider("LCT-1V", mmuxStream);
        
        // Ensure the cache is initialized
        var initialModels = (await service.GetAvailableModels()).ToList();
        initialModels.ShouldNotBeEmpty();

        // Act
        service.ClearCache();
        var modelsAfterClear = (await service.GetAvailableModels()).ToList();

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

        var service = new UnitCachingService([mockProvider1, mockProvider2]);

        // Act
        var models = (await service.GetAvailableModels()).ToList();

        // Assert
        models.ShouldContain("LCT-1V");
        models.ShouldContain("SHD-2D");
        models.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Service_ShouldHandleEmptyProviders()
    {
        // Arrange
        var service = new UnitCachingService([]);

        // Act
        var models = await service.GetAvailableModels();

        // Assert
        models.ShouldBeEmpty();
    }
}
