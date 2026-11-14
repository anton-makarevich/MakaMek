using Sanet.MakaMek.Assets.Models.Terrains;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Assets.Tests.Services;

public class TerrainImageResolverTests
{
    private readonly TerrainImageResolver _sut = new();

    [Fact]
    public void ResolveBase_ShouldRequestBaseAsset_WithoutVariant()
    {
        // Act
        var request = _sut.ResolveBase();

        // Assert
        request.AssetType.ShouldBe(TerrainAssetType.Base);
        request.AssetName.ShouldBe("base");
        request.Variant.ShouldBeNull();
        request.SelectionSeed.ShouldBe(0);
    }

    [Fact]
    public void ResolveBase_ShouldPreserveExplicitVariant()
    {
        // Act
        var request = _sut.ResolveBase(3);

        // Assert
        request.Variant.ShouldBe(3);
    }

    [Fact]
    public void ResolveOverlay_ShouldRequestOverlayAsset_ForTerrainType()
    {
        // Act
        var request = _sut.ResolveOverlay("lightwoods", 2);

        // Assert
        request.AssetType.ShouldBe(TerrainAssetType.Overlay);
        request.AssetName.ShouldBe("lightwoods");
        request.Variant.ShouldBe(2);
        request.SelectionSeed.ShouldBe(0);
    }

    [Theory]
    [InlineData(TerrainAssetType.Base)]
    [InlineData(TerrainAssetType.Overlay)]
    [InlineData(TerrainAssetType.Water)]
    [InlineData(TerrainAssetType.Road)]
    public void ResolveEdge_ShouldReturnNull_ForNonEdgeAssetType(TerrainAssetType assetType)
    {
        // Act
        var request = _sut.ResolveEdge(HexDirection.Top, assetType, new HexCoordinates(0, 0));

        // Assert
        request.ShouldBeNull();
    }

    [Fact]
    public void ResolveEdge_ShouldRequestEdgeAsset_ByNameOfDirection()
    {
        // Act
        var request = _sut.ResolveEdge(HexDirection.TopRight, TerrainAssetType.EdgeBottom, new HexCoordinates(2, 5));

        // Assert
        request.ShouldNotBeNull();
        request.Value.AssetType.ShouldBe(TerrainAssetType.EdgeBottom);
        request.Value.AssetName.ShouldBe("1");
        request.Value.Variant.ShouldBeNull();
    }

    [Fact]
    public void ResolveEdge_ShouldSeedVariantSelectionFromCoordinates()
    {
        // Arrange
        var coordinates = new HexCoordinates(4, 7);

        // Act
        var request = _sut.ResolveEdge(HexDirection.BottomLeft, TerrainAssetType.EdgeTop, coordinates);

        // Assert — Q + R * 31 keeps the per-hex seed stable and spread across rows
        request.ShouldNotBeNull();
        request.Value.SelectionSeed.ShouldBe(4 + 7 * 31);
    }

    [Theory]
    [InlineData(0, "000000")]
    [InlineData(1, "000001")]
    [InlineData(3, "000011")]
    [InlineData(63, "111111")]
    public void ResolveWaterTexture_ShouldRequestSixDigitBitmaskName(int mask, string expectedName)
    {
        // Act
        var request = _sut.ResolveWaterTexture(new CanonicalBitmaskResult((byte)mask, 0));

        // Assert
        request.AssetType.ShouldBe(TerrainAssetType.Water);
        request.AssetName.ShouldBe(expectedName);
        request.Variant.ShouldBeNull();
    }

    [Theory]
    [InlineData(0, "000000")]
    [InlineData(1, "000001")]
    [InlineData(3, "000011")]
    [InlineData(63, "111111")]
    public void ResolveRoadTexture_ShouldRequestSixDigitBitmaskName(int mask, string expectedName)
    {
        // Act
        var request = _sut.ResolveRoadTexture(new CanonicalBitmaskResult((byte)mask, 0));

        // Assert
        request.AssetType.ShouldBe(TerrainAssetType.Road);
        request.AssetName.ShouldBe(expectedName);
        request.Variant.ShouldBeNull();
    }
}
