using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Services;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class MapConfigViewModelTests
{
    private readonly MapConfigViewModel _sut;
    private readonly IMapPreviewRenderer _previewRenderer = Substitute.For<IMapPreviewRenderer>();
    private readonly IBattleMapFactory _mapFactory = Substitute.For<IBattleMapFactory>();
    private readonly IMapResourceProvider _mapResourceProvider = Substitute.For<IMapResourceProvider>();
    private readonly IFileService _fileService = Substitute.For<IFileService>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IDispatcherService _dispatcherService = Substitute.For<IDispatcherService>();
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    private const string TestBiome = "makamek.biomes.desert";
    
    public MapConfigViewModelTests()
    {
        _mapFactory.GenerateMap(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<ITerrainGenerator>())
            .Returns(ci => new BattleMap(ci.ArgAt<int>(0), ci.ArgAt<int>(1)));
        
        // Configure the dispatcher to execute actions immediately
        _dispatcherService.RunOnUIThread(Arg.InvokeDelegate<Func<Task>>());
        
        // Configure localization service mock - return the key if not configured
        _localizationService.GetString(Arg.Is<string>(k => k != "MapConfig_Width_Formatted" && k != "MapConfig_Height_Formatted" && k != "MapConfig_ForestCoverage_Formatted" && k != "MapConfig_LightWoods_Formatted" && k != "MapConfig_HillCoverage_Formatted" && k != "MapConfig_MaxElevation_Formatted" && k != "MapConfig_RoughCoverage_Formatted")).Returns(callInfo => callInfo.Arg<string>());
        _localizationService.GetString("MapConfig_Width_Formatted").Returns("Width: {0} hexes");
        _localizationService.GetString("MapConfig_Height_Formatted").Returns("Height: {0} hexes");
        _localizationService.GetString("MapConfig_ForestCoverage_Formatted").Returns("Forest Coverage: {0}%");
        _localizationService.GetString("MapConfig_LightWoods_Formatted").Returns("Light Woods: {0}%");
        _localizationService.GetString("MapConfig_HillCoverage_Formatted").Returns("Hill Coverage: {0}%");
        _localizationService.GetString("MapConfig_MaxElevation_Formatted").Returns("Max Elevation: {0}");
        _localizationService.GetString("MapConfig_RoughCoverage_Formatted").Returns("Rough Coverage: {0}%");
        
        _sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);
    }

    private static BattleMapData CreateSingleMapData() =>
        new()
        {
            Biome = TestBiome,
            HexData =
            [
                new()
                {
                    Coordinates = new HexCoordinateData(1, 1),
                    TerrainTypes = [MakaMekTerrains.Clear],
                    Level = 0
                }
            ]
        };

    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        _sut.MapWidth.ShouldBe(15);
        _sut.MapHeight.ShouldBe(17);
        _sut.ForestCoverage.ShouldBe(20);
        _sut.LightWoodsPercentage.ShouldBe(30);
        _sut.IsLightWoodsEnabled.ShouldBeTrue();
        _sut.RoughCoverage.ShouldBe(10);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(50, true)]
    public void ForestCoverage_WhenChanged_UpdatesLightWoodsEnabled(int coverage, bool expectedEnabled)
    {
        _sut.ForestCoverage = coverage;

        _sut.IsLightWoodsEnabled.ShouldBe(expectedEnabled);
    }

    [Fact]
    public void MapWidth_SetAndGet_ShouldUpdateCorrectly()
    {
        var newWidth = 20;

        _sut.MapWidth = newWidth;

        _sut.MapWidth.ShouldBe(newWidth);
    }

    [Fact]
    public void MapHeight_SetAndGet_ShouldUpdateCorrectly()
    {
        var newHeight = 25;

        _sut.MapHeight = newHeight;

        _sut.MapHeight.ShouldBe(newHeight);
    }

    [Fact]
    public void ForestCoverage_SetAndGet_ShouldUpdateCorrectly()
    {
        var newCoverage = 35;

        _sut.ForestCoverage = newCoverage;

        _sut.ForestCoverage.ShouldBe(newCoverage);
    }

    [Fact]
    public void LightWoodsPercentage_SetAndGet_ShouldUpdateCorrectly()
    {
        const int newPercentage = 60;

        _sut.LightWoodsPercentage = newPercentage;

        _sut.LightWoodsPercentage.ShouldBe(newPercentage);
    }
    
    [Fact]
    public void HillCoverage_SetAndGet_ShouldUpdateCorrectly()
    {
        const int newCoverage = 35;

        _sut.HillCoverage = newCoverage;

        _sut.HillCoverage.ShouldBe(newCoverage);
    }
    
    [Fact]
    public void MaxElevation_SetAndGet_ShouldUpdateCorrectly()
    {
        const int newElevation = 3;

        _sut.MaxElevation = newElevation;

        _sut.MaxElevation.ShouldBe(newElevation);
    }

    [Fact]
    public void MapWidthFormatted_ReturnsFormattedValue()
    {
        _sut.MapWidthFormatted.ShouldBe("Width: 15 hexes");
    }

    [Fact]
    public void MapHeightFormatted_ReturnsFormattedValue()
    {
        _sut.MapHeightFormatted.ShouldBe("Height: 17 hexes");
    }

    [Fact]
    public void ForestCoverageFormatted_ReturnsFormattedValue()
    {
        _sut.ForestCoverageFormatted.ShouldBe("Forest Coverage: 20%");
    }

    [Fact]
    public void LightWoodsFormatted_ReturnsFormattedValue()
    {
        _sut.LightWoodsFormatted.ShouldBe("Light Woods: 30%");
    }

    [Fact]
    public void HillCoverageFormatted_ReturnsFormattedValue()
    {
        _sut.HillCoverageFormatted.ShouldBe("Hill Coverage: 0%");
    }

    [Fact]
    public void MaxElevationFormatted_ReturnsFormattedValue()
    {
        _sut.MaxElevationFormatted.ShouldBe("Max Elevation: 2");
    }

    [Fact]
    public void MapWidthFormatted_UpdatesWhenMapWidthChanges()
    {
        _sut.MapWidth = 20;

        _sut.MapWidthFormatted.ShouldBe("Width: 20 hexes");
    }

    [Fact]
    public void MapHeightFormatted_UpdatesWhenMapHeightChanges()
    {
        _sut.MapHeight = 25;

        _sut.MapHeightFormatted.ShouldBe("Height: 25 hexes");
    }

    [Fact]
    public void ForestCoverageFormatted_UpdatesWhenForestCoverageChanges()
    {
        _sut.ForestCoverage = 35;

        _sut.ForestCoverageFormatted.ShouldBe("Forest Coverage: 35%");
    }

    [Fact]
    public void LightWoodsFormatted_UpdatesWhenLightWoodsPercentageChanges()
    {
        _sut.LightWoodsPercentage = 50;

        _sut.LightWoodsFormatted.ShouldBe("Light Woods: 50%");
    }

    [Fact]
    public void HillCoverageFormatted_UpdatesWhenHillCoverageChanges()
    {
        _sut.HillCoverage = 15;

        _sut.HillCoverageFormatted.ShouldBe("Hill Coverage: 15%");
    }

    [Fact]
    public void MaxElevationFormatted_UpdatesWhenMaxElevationChanges()
    {
        _sut.MaxElevation = 4;

        _sut.MaxElevationFormatted.ShouldBe("Max Elevation: 4");
    }

    [Fact]
    public void RoughCoverage_SetAndGet_ShouldUpdateCorrectly()
    {
        const int newCoverage = 15;

        _sut.RoughCoverage = newCoverage;

        _sut.RoughCoverage.ShouldBe(newCoverage);
    }

    [Fact]
    public void RoughCoverageFormatted_ReturnsFormattedValue()
    {
        _sut.RoughCoverageFormatted.ShouldBe("Rough Coverage: 10%");
    }

    [Fact]
    public void RoughCoverageFormatted_UpdatesWhenRoughCoverageChanges()
    {
        _sut.RoughCoverage = 20;

        _sut.RoughCoverageFormatted.ShouldBe("Rough Coverage: 20%");
    }

    [Fact]
    public void MaxRoughCoverage_DefaultsTo100MinusForestCoverage()
    {
        // ForestCoverage defaults to 20
        _sut.MaxRoughCoverage.ShouldBe(80);
    }

    [Fact]
    public void MaxRoughCoverage_UpdatesWhenForestCoverageChanges()
    {
        _sut.ForestCoverage = 50;

        _sut.MaxRoughCoverage.ShouldBe(50);
    }

    [Fact]
    public void ForestCoverage_WhenChanged_NotifiesMaxRoughCoverage()
    {
        var notified = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MapConfigViewModel.MaxRoughCoverage))
                notified = true;
        };

        _sut.ForestCoverage = 30;

        notified.ShouldBeTrue();
    }

    [Fact]
    public void ForestCoverage_WhenChanged_NotifiesRoughCoverageFormatted()
    {
        var notified = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MapConfigViewModel.RoughCoverageFormatted))
                notified = true;
        };

        _sut.ForestCoverage = 30;

        notified.ShouldBeTrue();
    }

    [Fact]
    public void MaxForestCoverage_DefaultsTo100MinusRoughCoverage()
    {
        _sut.MaxForestCoverage.ShouldBe(100 - _sut.RoughCoverage);
    }

    [Fact]
    public void MaxForestCoverage_UpdatesWhenRoughCoverageChanges()
    {
        _sut.RoughCoverage = 30;

        _sut.MaxForestCoverage.ShouldBe(70);
    }

    [Fact]
    public void RoughCoverage_WhenChanged_NotifiesMaxForestCoverage()
    {
        var notified = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MapConfigViewModel.MaxForestCoverage))
                notified = true;
        };

        _sut.RoughCoverage = 20;

        notified.ShouldBeTrue();
    }

    [Fact]
    public async Task Constructor_GeneratesInitialPreview()
    {
        // Arrange - setup mock to return a completed task
        var objectImage = new object();
        _previewRenderer.GeneratePreviewAsync(
            Arg.Any<BattleMap>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult<object?>(objectImage));
            
        // Act - create a new instance
        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);
        
        // Allow any pending async operations to complete
        var i = 0;
        while (sut.IsGenerating)
        {
            await Task.Delay(10);
            i++;
            if (i > 10) throw new TimeoutException("Preview generation timed out");
        }

        // Assert - initial preview should be generated
        await _previewRenderer.Received().GeneratePreviewAsync(
            Arg.Any<BattleMap>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
        sut.SelectedTabIndex = 1; // Switch to the Generate tab to access the generated map
        sut.Map.ShouldNotBeNull();
        sut.IsGenerating.ShouldBeFalse();
    }

    [Fact]
    public async Task PreviewImage_IsNotNull_AfterConstruction()
    {
        // Arrange
        var mockImage = new object();
        _previewRenderer.GeneratePreviewAsync(
            Arg.Any<BattleMap>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult<object?>(mockImage));

        // Act
        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);
        
        // Allow any pending async operations to complete
        await Task.Delay(100);

        // Assert
        sut.PreviewImage.ShouldBe(mockImage);
    }

    [Fact]
    public async Task MapHeight_Changed_GeneratesNewPreview()
    {
        // Arrange
        var mockImage1 = Substitute.For<IDisposable>();
        var mockImage2 = Substitute.For<IDisposable>();
        _previewRenderer.GeneratePreviewAsync(
            Arg.Any<BattleMap>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<object?>(mockImage1),
            Task.FromResult<object?>(mockImage2));

        // Act
        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);
        var i = 0;
        while (sut.IsGenerating)
        {
            await Task.Delay(10);
            i++;
            if (i > 10) throw new TimeoutException("Preview generation timed out");
        }
        sut.PreviewImage.ShouldBe(mockImage1);
        sut.MapHeight = 20;
        sut.IsGenerating.ShouldBeTrue();
        sut.PreviewImage.ShouldBe(mockImage1);

        // Wait for the delay to complete
        i = 0;
        while (sut.IsGenerating)
        {
            await Task.Delay(10);
            i++;
            if (i > 100) throw new TimeoutException("Preview generation timed out");
        }

        // Assert
        sut.PreviewImage.ShouldBe(mockImage2);
        sut.IsGenerating.ShouldBeFalse();
        mockImage1.Received().Dispose();
    }

    [Fact]
    public async Task PreviewImage_RemainsNull_WhenGeneratePreviewReturnsNull()
    {
        // Arrange
        _previewRenderer.GeneratePreviewAsync(
            Arg.Any<BattleMap>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult<object?>(null));

        // Act
        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);
        var i = 0;
        while (sut.IsGenerating)
        {
            await Task.Delay(10);
            i++;
            if (i > 100) throw new TimeoutException("Preview generation timed out");
        }

        // Assert
        sut.PreviewImage.ShouldBeNull();
        sut.SelectedTabIndex = 1; // Switch to the Generate tab to access the generated map
        sut.Map.ShouldNotBeNull();
        sut.IsGenerating.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadAvailableMapsAsync_PreselectsFirstMap()
    {
        // Arrange
        var mapData = CreateSingleMapData();
        _mapResourceProvider.GetAvailableMapsAsync()
            .Returns(new List<(string Name, BattleMapData MapData)>
            {
                ("Map1", mapData),
                ("Map2", mapData)
            });
        var map = new BattleMap(5, 5);
        _mapFactory.CreateFromData(Arg.Any<BattleMapData>()).Returns(map);
        
        var previewImage = new object();
        _previewRenderer.GeneratePreviewAsync(
            Arg.Any<BattleMap>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult<object?>(previewImage));

        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);

        // Act
        await sut.LoadAvailableMaps();

        // Assert
        sut.AvailableMaps.Count.ShouldBe(2);
        sut.AvailableMaps[0].PreviewImage.ShouldBe(previewImage);
        sut.SelectedMap.ShouldNotBeNull();
        sut.SelectedMap.Name.ShouldBe("Map1");
        sut.SelectedMap.IsSelected.ShouldBeTrue();
    }

    [Fact]
    public async Task SelectMap_UpdatesSelectedMapAndDeselectsPrevious()
    {
        // Arrange
        var mapData = CreateSingleMapData();
        _mapResourceProvider.GetAvailableMapsAsync()
            .Returns(new List<(string Name, BattleMapData MapData)>
            {
                ("Map1", mapData),
                ("Map2", mapData)
            });

        var map = new BattleMap(5, 5);
        _mapFactory.CreateFromData(Arg.Any<BattleMapData>()).Returns(map);

        var sut = new MapConfigViewModel(_previewRenderer,
            _mapFactory,
            _mapResourceProvider,
            _fileService,
            _logger, _dispatcherService, _localizationService);
        await sut.LoadAvailableMaps();

        // Act
        sut.SelectMap(sut.AvailableMaps[1]);

        // Assert
        sut.SelectedMap.ShouldBe(sut.AvailableMaps[1]);
        sut.AvailableMaps[0].IsSelected.ShouldBeFalse();
        sut.AvailableMaps[1].IsSelected.ShouldBeTrue();
    }

    [Fact]
    public async Task Map_ReturnsSelectedMap_WhenTabIndexIsZero()
    {
        // Arrange
        var mapData = CreateSingleMapData();
        _mapResourceProvider.GetAvailableMapsAsync()
            .Returns(new List<(string Name, BattleMapData MapData)>
            {
                ("TestMap", mapData)
            });
        var map = new BattleMap(5, 5);
        _mapFactory.CreateFromData(Arg.Any<BattleMapData>()).Returns(map);

        var sut = new MapConfigViewModel(_previewRenderer,
            _mapFactory,
            _mapResourceProvider,
            _fileService,
            _logger, _dispatcherService, _localizationService);
        await sut.LoadAvailableMaps();

        // Act
        sut.SelectedTabIndex = 0;

        // Assert
        sut.Map.ShouldBe(map);
    }

    [Fact]
    public void Map_ReturnsGeneratedMap_WhenTabIndexIsOne()
    {
        // GeneratedMap is set synchronously (before the first await) inside UpdateMapAsync,
        // so this synchronous test is reliable without awaiting.
        // Act
        _sut.SelectedTabIndex = 1;

        // Assert
        _sut.Map.ShouldNotBeNull();
    }

    [Fact]
    public void SelectedTabIndex_DefaultsToZero()
    {
        _sut.SelectedTabIndex.ShouldBe(0);
    }

    [Fact]
    public void AvailableMaps_InitiallyEmpty()
    {
        _sut.AvailableMaps.ShouldBeEmpty();
    }

    [Fact]
    public void IsLoadingMaps_DefaultsToFalse()
    {
        // Arrange
        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);

        // Assert
        sut.IsLoadingMaps.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadAvailableMapsAsync_HandlesException_AndSetsLoadingToFalse()
    {
        // Arrange
        _mapResourceProvider.GetAvailableMapsAsync()
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);

        // Act
        await sut.LoadAvailableMaps();

        // Assert
        sut.IsLoadingMaps.ShouldBeFalse();
        sut.AvailableMaps.ShouldBeEmpty();
        _logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<InvalidOperationException>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task Dispose_DisposesPreviewImage()
    {
        // Arrange
        var mockDisposable = Substitute.For<IDisposable>();
        _previewRenderer.GeneratePreviewAsync(
            Arg.Any<BattleMap>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult<object?>(mockDisposable));
        var mapData = CreateSingleMapData();
        _mapResourceProvider.GetAvailableMapsAsync()
            .Returns(new List<(string Name, BattleMapData MapData)>
            {
                ("TestMap", mapData)
            });
        var map = new BattleMap(5, 5);
        _mapFactory.CreateFromData(Arg.Any<BattleMapData>()).Returns(map);

        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);
        
        await sut.LoadAvailableMaps();

        // Act
        sut.Dispose();

        // Assert
        mockDisposable.Received(3).Dispose(); // one for the generated map, one for the initially loaded available map and one for the reloaded map
    }
    
    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);

        // Act & Assert - Should not throw
        sut.Dispose();
        sut.Dispose(); // Calling twice should be safe
    }

    [Fact]
    public async Task LoadMapCommand_LoadsMapAndSelects()
    {
        // Arrange
        const string mapJson = """
                               {
                                 "Biome": "makamek.biomes.grasslands",
                                 "HexData": [
                                 {
                                   "Coordinates": {
                                     "Q": 1,
                                     "R": 1
                                   },
                                   "TerrainTypes": [
                                     0
                                   ],
                                   "Level": 0
                                 }
                                 ]
                               }
                               """;
        const string fileName = "TestMap.json";
        _fileService.OpenFile(Arg.Any<string>()).Returns(Task.FromResult<(string? Name, string? Content)>((fileName, mapJson)));

        var expectedMap = new BattleMap(2, 2);
        _mapFactory.CreateFromData(Arg.Any<BattleMapData>()).Returns(expectedMap);
        
        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);
        
        // Act
        await sut.LoadMapCommand.ExecuteAsync();
        
        // Assert
        sut.AvailableMaps.ShouldContain(m => m.Name == "TestMap"); // .json should be trimmed
        sut.SelectedMap.ShouldNotBeNull();
        sut.SelectedMap!.Name.ShouldBe("TestMap");
        sut.SelectedMap.Map.ShouldBe(expectedMap);
        sut.SelectedTabIndex = 0; // ensure the Select Map tab is active
        sut.Map.ShouldBe(expectedMap);
    }

    [Fact]
    public async Task LoadMapCommand_WhenFileContentIsEmpty_DoesNothing()
    {
        // Arrange
        _fileService.OpenFile(Arg.Any<string>())
            .Returns(Task.FromResult<(string? Name, string? Content)>(("TestMap.json", "   ")));

        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);

        // Act
        await sut.LoadMapCommand.ExecuteAsync();

        // Assert
        sut.AvailableMaps.Count.ShouldBe(0);
        sut.SelectedMap.ShouldBeNull();
        _mapFactory.DidNotReceive().CreateFromData(Arg.Any<BattleMapData>());
    }

    [Fact]
    public async Task LoadMapCommand_WhenHexDataIsEmpty_DoesNothing()
    {
        // Arrange
        _fileService.OpenFile(Arg.Any<string>())
            .Returns(Task.FromResult<(string? Name, string? Content)>(("TestMap.json", """{"HexData": []}""")));

        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);

        // Act
        await sut.LoadMapCommand.ExecuteAsync();

        // Assert
        sut.AvailableMaps.Count.ShouldBe(0);
        sut.SelectedMap.ShouldBeNull();
        _mapFactory.DidNotReceive().CreateFromData(Arg.Any<BattleMapData>());
    }

    [Fact]
    public async Task LoadMapCommand_WhenOpenFileThrows_LogsErrorAndDoesNotThrow()
    {
        // Arrange
        var ex = new InvalidOperationException("boom");
        _fileService.OpenFile(Arg.Any<string>())
            .Returns(_ => Task.FromException<(string? Name, string? Content)>(ex));

        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);

        // Act
        await sut.LoadMapCommand.ExecuteAsync();

        // Assert
        sut.AvailableMaps.Count.ShouldBe(0);
        sut.SelectedMap.ShouldBeNull();
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            ex,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }
    
    [Fact]
    public async Task LoadMapCommand_WhenFileNameIsEmpty_UsesDefaultMapName()
    {
        // Arrange
        _fileService.OpenFile(Arg.Any<string>())
            .Returns(Task.FromResult<(string? Name, string? Content)>((null, "{\"HexData\":[{\"Coordinates\":{\"Q\":1,\"R\":1},\"TerrainTypes\":[0],\"Level\":0}]}")));

        _mapFactory.CreateFromData(Arg.Any<BattleMapData>()).Returns(new BattleMap(2, 2));

        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);

        // Act
        await sut.LoadMapCommand.ExecuteAsync();

        // Assert
        sut.AvailableMaps.ShouldContain(m => m.Name == "Loaded Map");
    }

    [Fact]
    public void LoadAvailableMaps_PopulatesItemsImmediately_ThenGeneratesPreviewsInParallel()
    {
        // Arrange
        var mapData = CreateSingleMapData();
        var maps = new List<(string Name, BattleMapData MapData)>
        {
            ("Map1", mapData),
            ("Map2", mapData),
            ("Map3", mapData)
        };
        _mapResourceProvider.GetAvailableMapsAsync().Returns(maps);

        var map = new BattleMap(5, 5);
        _mapFactory.CreateFromData(Arg.Any<BattleMapData>()).Returns(map);

        var previewImage = new object();
        var previewGenerationTasks = new List<Task<object?>>();

        _previewRenderer.GeneratePreviewAsync(
                Arg.Any<BattleMap>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var task = Task.FromResult<object?>(previewImage);
                previewGenerationTasks.Add(task);
                return task;
            });

        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger,
            _dispatcherService, _localizationService);

        // Items should be populated immediately
        sut.AvailableMaps.Count.ShouldBe(3); 
        sut.AvailableMaps.All(m => !string.IsNullOrEmpty(m.Name)).ShouldBeTrue();

        // Preselection should happen immediately
        sut.SelectedMap.ShouldNotBeNull();
        sut.SelectedMap.Name.ShouldBe("Map1");
        sut.SelectedMap.IsSelected.ShouldBeTrue();

        // Assert - all previews should be generated
        previewGenerationTasks.Count.ShouldBe(4);// 3 loaded and 1 generated
        sut.AvailableMaps.All(m => m.PreviewImage == previewImage).ShouldBeTrue();

        // Verify dispatcher was called for each preview
        _dispatcherService.Received(3).RunOnUIThread(Arg.Any<Func<Task>>());
    }

    [Fact]
    public async Task LoadAvailableMaps_WhenPreviewGenerationFails_LogsErrorAndLeavesPreviewNull()
    {
        // Arrange
        var mapData1 = new BattleMapData
        {
            HexData =
            [
                new()
                {
                    Coordinates = new HexCoordinateData(1, 1),
                    TerrainTypes = [MakaMekTerrains.Clear],
                    Level = 0
                }
            ]
        };

        var mapData2 = new BattleMapData
        {
            HexData =
            [
                new()
                {
                    Coordinates = new HexCoordinateData(2, 2),
                    TerrainTypes = [MakaMekTerrains.Clear],
                    Level = 0
                }
            ]
        };

        _mapResourceProvider.GetAvailableMapsAsync()
            .Returns(new List<(string Name, BattleMapData MapData)>
            {
                ("Map1", mapData1),
                ("Map2", mapData2)
            });

        var map1 = new BattleMap(5, 5);
        var map2 = new BattleMap(6, 6);

        _mapFactory.CreateFromData(mapData1).Returns(map1);
        _mapFactory.CreateFromData(mapData2).Returns(map2);
        
        _previewRenderer.GeneratePreviewAsync(
            Arg.Is<BattleMap>(x => x.Width == 5 && x.Height == 5),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromException<object?>(new InvalidOperationException("Preview failed")));
        
        _previewRenderer.GeneratePreviewAsync(
            Arg.Is<BattleMap>(x => x.Width == 6 && x.Height == 6),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<object?>(new object()));

        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);
        
        // Act
        await sut.LoadAvailableMaps();

        // Assert
        sut.AvailableMaps.Count.ShouldBe(2);
        
        // Map1 should have a null preview due to an error
        var map1Item = sut.AvailableMaps.First(m => m.Name == "Map1");
        map1Item.PreviewImage.ShouldBeNull();
        
        // Map2 should have a preview
        var map2Item = sut.AvailableMaps.First(m => m.Name == "Map2");
        map2Item.PreviewImage.ShouldNotBeNull();
        
        // Error should be logged for Map1
        _logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<InvalidOperationException>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task LoadAvailableMaps_WhenNoMapsAvailable_DoesNotThrow()
    {
        // Arrange
        _mapResourceProvider.GetAvailableMapsAsync()
            .Returns(new List<(string Name, BattleMapData MapData)>());

        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);

        // Act & Assert - Should not throw
        await sut.LoadAvailableMaps();
        
        sut.AvailableMaps.ShouldBeEmpty();
        sut.SelectedMap.ShouldBeNull();
        sut.IsLoadingMaps.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateMap_CancelsPreviousOperation_WhenCalledMultipleTimes()
    {
        // Arrange
        var secondPreview = new object();
        var callCount = 0;
        var firstCallTcs = new TaskCompletionSource<object?>();

        _previewRenderer.GeneratePreviewAsync(
                Arg.Any<BattleMap>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var token = callInfo.ArgAt<CancellationToken>(2);
                if (Interlocked.Increment(ref callCount) != 1) return Task.FromResult<object?>(secondPreview);
                // Block the first call; resolve as canceled when the token fires
                token.Register(() => firstCallTcs.TrySetCanceled(token));
                return firstCallTcs.Task;

            });

        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);

        // Act - Trigger multiple rapid updates
        sut.MapWidth = 20; // First update
        await Task.Delay(10); // Small delay
        sut.MapHeight = 25; // Second update (should cancel first)

        // Wait for operations to complete
        var i = 0;
        while (sut.IsGenerating)
        {
            await Task.Delay(10);
            i++;
            if (i > 100) throw new TimeoutException("Operations timed out");
        }

        // Assert - Should complete successfully without throwing
        sut.IsGenerating.ShouldBeFalse();
        sut.PreviewImage.ShouldBe(secondPreview);
    }

    [Fact]
    public async Task Dispose_CancelsPendingOperations()
    {
        // Arrange
        var previewTask = new TaskCompletionSource<object?>();
        CancellationToken capturedToken = CancellationToken.None;
        _previewRenderer.GeneratePreviewAsync(
                Arg.Any<BattleMap>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedToken = callInfo.ArgAt<CancellationToken>(2);
                return previewTask.Task;
            });

        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);
        
        // The constructor's UpdateMap() calls GeneratePreviewAsync synchronously before awaiting;
        // capturedToken is set by the time the constructor returns.
        // If for any reason it isn't set yet, wait briefly.
        var i = 0;
        while (capturedToken == CancellationToken.None && i < 50)
        {
            await Task.Delay(10);
            i++;
        }

        // Act
        sut.Dispose();

        // Assert - the token passed to the renderer must be canceled
        capturedToken.IsCancellationRequested.ShouldBeTrue();
    }
    
    [Fact]
    public async Task HillCoverage_WhenGreaterThanZero_GeneratesMapWithHills()
    {
        // Arrange
        var mockImage = new object();
        _previewRenderer.GeneratePreviewAsync(
            Arg.Any<BattleMap>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult<object?>(mockImage));

        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _fileService, _logger, _dispatcherService, _localizationService);

        // Wait for the initial generation to complete
        var i = 0;
        while (sut.IsGenerating)
        {
            await Task.Delay(10);
            i++;
            if (i > 10) throw new TimeoutException("Initial generation timed out");
        }

        // Act - set hill coverage > 0 to trigger hill generation
        sut.HillCoverage = 25;
        sut.MaxElevation = 3;

        // Wait for a generation to complete
        i = 0;
        while (sut.IsGenerating)
        {
            await Task.Delay(10);
            i++;
            if (i > 100) throw new TimeoutException("Hill generation timed out");
        }

        // Assert
        sut.SelectedTabIndex = 1; // Switch to the Generate tab
        sut.Map.ShouldNotBeNull();
        _mapFactory.Received().GenerateMap(sut.MapWidth, sut.MapHeight, Arg.Any<ITerrainGenerator>());
    }
}
