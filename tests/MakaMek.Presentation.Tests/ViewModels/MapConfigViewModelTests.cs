using NSubstitute;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MakaMek.Presentation.ViewModels;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class MapConfigViewModelTests
{
    private readonly MapConfigViewModel _sut;
    private readonly IMapPreviewRenderer _previewRenderer = Substitute.For<IMapPreviewRenderer>();
    private readonly IBattleMapFactory _mapFactory = Substitute.For<IBattleMapFactory>();

    public MapConfigViewModelTests()
    {
        _mapFactory.GenerateMap(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<ITerrainGenerator>())
            .Returns(ci => new BattleMap(ci.ArgAt<int>(0), ci.ArgAt<int>(1)));
        _sut = new MapConfigViewModel(_previewRenderer, _mapFactory);
    }

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
        // Arrange - setup mock to return completed task
        _previewRenderer.GeneratePreviewAsync(
            Arg.Any<BattleMap>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult<object?>(new object()));
            
        // Act - create a new instance
        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory);
        
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
        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory);
        
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
        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory);
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

        // Wait for delay to complete
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
        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory);
        var i = 0;
        while (sut.IsGenerating)
        {
            await Task.Delay(10);
            i++;
            if (i > 100) throw new TimeoutException("Preview generation timed out");
        }

        // Assert
        sut.PreviewImage.ShouldBeNull();
        sut.Map.ShouldNotBeNull();
        sut.IsGenerating.ShouldBeFalse();
    }
}

