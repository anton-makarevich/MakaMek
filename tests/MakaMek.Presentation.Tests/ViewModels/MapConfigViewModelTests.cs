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
    public void Constructor_GeneratesInitialPreview()
    {
        // Assert - initial preview should be generated
        _previewRenderer.Received(1).GeneratePreview(
            Arg.Any<BattleMap>(),
            Arg.Any<int>(),
            Arg.Any<int>());
    }

    [Fact]
    public void PreviewImage_IsNotNull_AfterConstruction()
    {
        // Arrange
        var mockImage = new object();
        _previewRenderer.GeneratePreview(
            Arg.Any<BattleMap>(),
            Arg.Any<int>(),
            Arg.Any<int>()).Returns(mockImage);

        // Act
        var sut = new MapConfigViewModel(_previewRenderer, _mapFactory);

        // Assert
        sut.PreviewImage.ShouldBe(mockImage);
    }
}

