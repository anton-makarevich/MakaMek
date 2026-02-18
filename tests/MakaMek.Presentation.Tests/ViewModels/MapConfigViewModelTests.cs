using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sanet.MakaMek.Core.Services;
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
    private readonly ILogger _logger = Substitute.For<ILogger>();

    public MapConfigViewModelTests()
    {
        _mapFactory.GenerateMap(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<ITerrainGenerator>())
            .Returns(ci => new BattleMap(ci.ArgAt<int>(0), ci.ArgAt<int>(1)));
        _sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _logger);
    }

    private static IList<HexData> CreateSingleHexData() =>
    [
        new()
        {
            Coordinates = new HexCoordinateData(1, 1),
            TerrainTypes = [MakaMekTerrains.Clear],
            Level = 0
        }
    ];

    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        _sut.MapWidth.ShouldBe(15);
        _sut.MapHeight.ShouldBe(17);
        _sut.ForestCoverage.ShouldBe(20);
        _sut.LightWoodsPercentage.ShouldBe(30);
        _sut.IsLightWoodsEnabled.ShouldBeTrue();
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
        var newPercentage = 60;

        _sut.LightWoodsPercentage = newPercentage;

        _sut.LightWoodsPercentage.ShouldBe(newPercentage);
    }

    [Fact]
    public void MapWidthLabel_ReturnsCorrectValue()
    {
        _sut.MapWidthLabel.ShouldBe("Map Width");
    }

    [Fact]
    public void MapHeightLabel_ReturnsCorrectValue()
    {
        _sut.MapHeightLabel.ShouldBe("Map Height");
    }

    [Fact]
    public void ForestCoverageLabel_ReturnsCorrectValue()
    {
        _sut.ForestCoverageLabel.ShouldBe("Forest Coverage");
    }

    [Fact]
    public void LightWoodsLabel_ReturnsCorrectValue()
    {
        _sut.LightWoodsLabel.ShouldBe("Light Woods Percentage");
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
        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _logger);
        
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
        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _logger);
        
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
        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _logger);
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
        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _logger);
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
        var hexData = CreateSingleHexData();
        _mapResourceProvider.GetAvailableMapsAsync()
            .Returns(new List<(string Name, IList<HexData> HexData)>
            {
                ("Map1", hexData),
                ("Map2", hexData)
            });
        var map = new BattleMap(5, 5);
        _mapFactory.CreateFromData(Arg.Any<IList<HexData>>()).Returns(map);
        
        var previewImage = new object();
        _previewRenderer.GeneratePreviewAsync(
            Arg.Any<BattleMap>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult<object?>(previewImage));

        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _logger);

        // Act
        await sut.LoadAvailableMapsAsync();

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
        var hexData = CreateSingleHexData();
        _mapResourceProvider.GetAvailableMapsAsync()
            .Returns(new List<(string Name, IList<HexData> HexData)>
            {
                ("Map1", hexData),
                ("Map2", hexData)
            });
        
        var map = new BattleMap(5, 5);
        _mapFactory.CreateFromData(Arg.Any<IList<HexData>>()).Returns(map);

        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _logger);
        await sut.LoadAvailableMapsAsync();

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
        var hexData = CreateSingleHexData();
        _mapResourceProvider.GetAvailableMapsAsync()
            .Returns(new List<(string Name, IList<HexData> HexData)>
            {
                ("TestMap", hexData)
            });
        var map = new BattleMap(5, 5);
        _mapFactory.CreateFromData(Arg.Any<IList<HexData>>()).Returns(map);

        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _logger);
        await sut.LoadAvailableMapsAsync();

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
        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _logger);

        // Assert
        sut.IsLoadingMaps.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadAvailableMapsAsync_HandlesException_AndSetsLoadingToFalse()
    {
        // Arrange
        _mapResourceProvider.GetAvailableMapsAsync()
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _logger);

        // Act
        await sut.LoadAvailableMapsAsync();

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
        var hexData = CreateSingleHexData();
        _mapResourceProvider.GetAvailableMapsAsync()
            .Returns(new List<(string Name, IList<HexData> HexData)>
            {
                ("TestMap", hexData)
            });
        var map = new BattleMap(5, 5);
        _mapFactory.CreateFromData(Arg.Any<IList<HexData>>()).Returns(map);

        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _logger);
        
        await sut.LoadAvailableMapsAsync();

        // Act
        sut.Dispose();

        // Assert
        mockDisposable.Received(3).Dispose(); // one for the generated map, one for the initially loaded available map and one for the reloaded map
    }
    
    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory, _mapResourceProvider, _logger);

        // Act & Assert - Should not throw
        sut.Dispose();
        sut.Dispose(); // Calling twice should be safe
    }
}
