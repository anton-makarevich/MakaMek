using System.IO.Compression;
using System.Text.Json;
using Sanet.MakaMek.Core.Data.Serialization.Converters;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services;

public class UnitCachingServiceTests
{
    [Fact]
    public void GetAvailableModels_ShouldReturnModels_WhenInitialized()
    {
        // Arrange
        var service = new UnitCachingService();

        // Act
        var models = service.GetAvailableModels().ToList();

        // Assert
        models.ShouldNotBeEmpty();
    }

    [Fact]
    public void GetAllUnits_ShouldReturnUnits_WhenInitialized()
    {
        // Arrange
        var service = new UnitCachingService();
        service.SetHostAssembly(typeof(UnitCachingServiceTests).Assembly);

        // Act
        var units = service.GetAllUnits().ToList();

        // Assert
        units.ShouldNotBeEmpty();
        units.ShouldAllBe(unit => !string.IsNullOrEmpty(unit.Model));
        units.ShouldAllBe(unit => !string.IsNullOrEmpty(unit.Chassis));
    }

    [Fact]
    public void GetUnitData_ShouldReturnUnit_WhenModelExists()
    {
        // Arrange
        var service = new UnitCachingService();
        var availableModels = service.GetAvailableModels().ToList();
        
        // Skip test if no models are available
        if (!availableModels.Any())
        {
            return;
        }

        var firstModel = availableModels.First();

        // Act
        var unitData = service.GetUnitData(firstModel);

        // Assert
        unitData.ShouldNotBeNull();
        unitData.Value.Model.ShouldBe(firstModel);
    }

    [Fact]
    public void GetUnitData_ShouldReturnNull_WhenModelDoesNotExist()
    {
        // Arrange
        var service = new UnitCachingService();

        // Act
        var unitData = service.GetUnitData("NonExistentModel");

        // Assert
        unitData.ShouldBeNull();
    }

    [Fact]
    public void GetUnitImage_ShouldReturnImageBytes_WhenModelExists()
    {
        // Arrange
        var service = new UnitCachingService();
        var availableModels = service.GetAvailableModels().ToList();
        
        // Skip test if no models are available
        if (!availableModels.Any())
        {
            return;
        }

        var firstModel = availableModels.First();

        // Act
        var imageBytes = service.GetUnitImage(firstModel);

        // Assert
        imageBytes.ShouldNotBeNull();
        imageBytes.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void GetUnitImage_ShouldReturnNull_WhenModelDoesNotExist()
    {
        // Arrange
        var service = new UnitCachingService();

        // Act
        var imageBytes = service.GetUnitImage("NonExistentModel");

        // Assert
        imageBytes.ShouldBeNull();
    }

    [Fact]
    public void ClearCache_ShouldClearAllData()
    {
        // Arrange
        var service = new UnitCachingService();

        // Ensure the cache is initialized
        service.GetAvailableModels();

        // Act
        service.ClearCache();
        var modelsAfterClear = service.GetAvailableModels().ToList();

        // Assert
        modelsAfterClear.ShouldBeEmpty();
    }

    [Fact]
    public void LoadUnitFromMmuxFile_ShouldWork()
    {
        // Arrange
        var mmuxFilePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "src", "MakaMek.Avalonia", "MakaMek.Avalonia", "Resources", "Units", "Mechs", "LCT-1V.mmux");

        // Skip test if a file doesn't exist
        if (!File.Exists(mmuxFilePath))
        {
            return;
        }

        // Act & Assert
        using var fileStream = File.OpenRead(mmuxFilePath);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

        // Verify MMUX structure
        var unitJsonEntry = archive.GetEntry("unit.json");
        unitJsonEntry.ShouldNotBeNull();

        var unitImageEntry = archive.GetEntry("unit.png");
        unitImageEntry.ShouldNotBeNull();

        var manifestEntry = archive.GetEntry("manifest.json");
        manifestEntry.ShouldNotBeNull();

        // Verify unit data can be deserialized
        using var unitJsonStream = unitJsonEntry.Open();
        using var reader = new StreamReader(unitJsonStream);
        var jsonContent = reader.ReadToEnd();

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

        var unitData = JsonSerializer.Deserialize<UnitData>(jsonContent, jsonOptions);
        unitData.Model.ShouldNotBeNullOrEmpty();
        unitData.Chassis.ShouldNotBeNullOrEmpty();

        // Verify image data
        using var imageStream = unitImageEntry.Open();
        using var memoryStream = new MemoryStream();
        imageStream.CopyTo(memoryStream);
        var imageBytes = memoryStream.ToArray();
        imageBytes.Length.ShouldBeGreaterThan(0);
    }
}
