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
    private static UnitCachingService CreateServiceWithMockProvider(string unitId, Stream mmuxStream)
    {
        var mockProvider = Substitute.For<IUnitStreamProvider>();
        mockProvider.GetAvailableUnitIds().Returns(new[] { unitId });
        mockProvider.GetUnitStream(unitId).Returns(mmuxStream);

        return new UnitCachingService(new[] { mockProvider });
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

            var jsonOptions = new JsonSerializerOptions
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

            var unitJsonEntry = archive.CreateEntry("unit.json");
            using (var entryStream = unitJsonEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var json = JsonSerializer.Serialize(unitData, jsonOptions);
                writer.Write(json);
            }

            // Create unit.png (minimal PNG data)
            var unitImageEntry = archive.CreateEntry("unit.png");
            using (var entryStream = unitImageEntry.Open())
            {
                // Write minimal PNG header (not a valid image, but sufficient for testing)
                var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
                entryStream.Write(pngHeader, 0, pngHeader.Length);
            }
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    [Fact]
    public void GetAvailableModels_ShouldReturnModels_WhenInitialized()
    {
        // Arrange
        using var mmuxStream = CreateTestMmuxStream("LCT-1V", "Locust");
        var service = CreateServiceWithMockProvider("LCT-1V", mmuxStream);

        // Act
        var models = service.GetAvailableModels().ToList();

        // Assert
        models.ShouldNotBeNull();
        models.ShouldContain("LCT-1V");
    }

    [Fact]
    public void GetUnitData_ShouldReturnUnitData_WhenModelExists()
    {
        // Arrange
        using var mmuxStream = CreateTestMmuxStream("LCT-1V", "Locust");
        var service = CreateServiceWithMockProvider("LCT-1V", mmuxStream);

        // Act
        var unitData = service.GetUnitData("LCT-1V");

        // Assert
        unitData.ShouldNotBeNull();
        unitData.Value.Model.ShouldBe("LCT-1V");
        unitData.Value.Chassis.ShouldBe("Locust");
    }

    [Fact]
    public void GetUnitImage_ShouldReturnImageBytes_WhenModelExists()
    {
        // Arrange
        using var mmuxStream = CreateTestMmuxStream("LCT-1V", "Locust");
        var service = CreateServiceWithMockProvider("LCT-1V", mmuxStream);

        // Act
        var imageBytes = service.GetUnitImage("LCT-1V");

        // Assert
        imageBytes.ShouldNotBeNull();
        imageBytes.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void GetAllUnits_ShouldReturnAllUnits_WhenInitialized()
    {
        // Arrange
        using var mmuxStream = CreateTestMmuxStream("LCT-1V", "Locust");
        var service = CreateServiceWithMockProvider("LCT-1V", mmuxStream);

        // Act
        var units = service.GetAllUnits().ToList();

        // Assert
        units.ShouldNotBeEmpty();
        units.ShouldContain(u => u.Model == "LCT-1V");
    }

    [Fact]
    public void ClearCache_ShouldClearAllData()
    {
        // Arrange
        using var mmuxStream = CreateTestMmuxStream("LCT-1V", "Locust");
        var service = CreateServiceWithMockProvider("LCT-1V", mmuxStream);
        
        // Ensure cache is initialized
        var initialModels = service.GetAvailableModels().ToList();
        initialModels.ShouldNotBeEmpty();

        // Act
        service.ClearCache();
        var modelsAfterClear = service.GetAvailableModels().ToList();

        // Assert
        modelsAfterClear.ShouldBeEmpty();
    }

    [Fact]
    public void Service_ShouldHandleMultipleProviders()
    {
        // Arrange
        var mockProvider1 = Substitute.For<IUnitStreamProvider>();
        mockProvider1.GetAvailableUnitIds().Returns(new[] { "LCT-1V" });
        using var mmuxStream1 = CreateTestMmuxStream("LCT-1V", "Locust");
        mockProvider1.GetUnitStream("LCT-1V").Returns(mmuxStream1);

        var mockProvider2 = Substitute.For<IUnitStreamProvider>();
        mockProvider2.GetAvailableUnitIds().Returns(new[] { "SHD-2D" });
        using var mmuxStream2 = CreateTestMmuxStream("SHD-2D", "Shadowhawk");
        mockProvider2.GetUnitStream("SHD-2D").Returns(mmuxStream2);

        var service = new UnitCachingService(new[] { mockProvider1, mockProvider2 });

        // Act
        var models = service.GetAvailableModels().ToList();

        // Assert
        models.ShouldContain("LCT-1V");
        models.ShouldContain("SHD-2D");
        models.Count.ShouldBe(2);
    }

    [Fact]
    public void Service_ShouldHandleEmptyProviders()
    {
        // Arrange
        var service = new UnitCachingService(Array.Empty<IUnitStreamProvider>());

        // Act
        var models = service.GetAvailableModels().ToList();

        // Assert
        models.ShouldBeEmpty();
    }
}
