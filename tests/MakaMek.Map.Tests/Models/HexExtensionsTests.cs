using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

public class HexExtensionsTests
{
    [Fact]
    public void GetWaterDepth_ReturnsNull_WhenNoWaterTerrain()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(1, 1));
        hex.AddTerrain(new ClearTerrain());

        // Act
        var depth = hex.GetWaterDepth();

        // Assert
        depth.ShouldBeNull();
    }

    [Theory]
    [InlineData(0, 0)]   // Shallow water (Height 0) -> Depth 0
    [InlineData(-1, 1)] // Standard depth (Height -1) -> Depth 1
    [InlineData(-2, 2)] // Deep water (Height -2) -> Depth 2
    [InlineData(-3, 3)] // Very deep water (Height -3) -> Depth 3
    public void GetWaterDepth_ReturnsCorrectDepth(int terrainHeight, int expectedDepth)
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(1, 1));
        hex.AddTerrain(new WaterTerrain(terrainHeight));

        // Act
        var depth = hex.GetWaterDepth();

        // Assert
        depth.ShouldBe(expectedDepth);
    }
    
    [Fact]
    public void GetWaterDepth_ReturnsCorrectDepth_WhenHexHasMultipleTerrains()
    {
        // Arrange - hex with both clear and water terrain
        var hex = new Hex(new HexCoordinates(1, 1));
        hex.AddTerrain(new ClearTerrain());
        hex.AddTerrain(new WaterTerrain(-2));

        // Act
        var depth = hex.GetWaterDepth();

        // Assert
        depth.ShouldBe(2);
    }

    [Fact]
    public void GetElevationChangeTo_ReturnsZero_WhenToHexIsNull()
    {
        var hex = new Hex(new HexCoordinates(1, 1)) { Level = 5 };

        var result = hex.GetElevationChangeTo(null);

        result.ShouldBe(0);
    }

    [Fact]
    public void GetElevationChangeTo_ReturnsZero_WhenLevelsAreSame()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 3 };
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 3 };

        var result = fromHex.GetElevationChangeTo(toHex);

        result.ShouldBe(0);
    }

    [Fact]
    public void GetElevationChangeTo_ReturnsPositive_WhenAscending()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 2 };
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 5 };

        var result = fromHex.GetElevationChangeTo(toHex);

        result.ShouldBe(3);
    }

    [Fact]
    public void GetElevationChangeTo_ReturnsNegative_WhenDescending()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 5 };
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 2 };

        var result = fromHex.GetElevationChangeTo(toHex);

        result.ShouldBe(-3);
    }

    [Fact]
    public void GetElevationChangeTo_AccountsForWaterDepth()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 3 };
        fromHex.AddTerrain(new WaterTerrain(-1));
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 3 };

        var result = fromHex.GetElevationChangeTo(toHex);

        result.ShouldBe(1);
    }

    [Fact]
    public void GetElevationChangeTo_AccountsForDifferentWaterDepths()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 5 };
        fromHex.AddTerrain(new WaterTerrain(-2));
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 5 };
        toHex.AddTerrain(new WaterTerrain(-1));

        var result = fromHex.GetElevationChangeTo(toHex);

        result.ShouldBe(1);
    }

    [Fact]
    public void IsRoadOrPaved_ReturnsTrue_ForRoadTerrain()
    {
        var hex = new Hex(new HexCoordinates(1, 1));
        hex.AddTerrain(new RoadTerrain());

        var result = hex.IsRoadOrPaved();

        result.ShouldBeTrue();
    }

    [Fact]
    public void IsRoadOrPaved_ReturnsTrue_ForPavementTerrain()
    {
        var hex = new Hex(new HexCoordinates(1, 1));
        hex.AddTerrain(new PavementTerrain());

        var result = hex.IsRoadOrPaved();

        result.ShouldBeTrue();
    }

    [Fact]
    public void IsRoadOrPaved_ReturnsTrue_ForBridgeTerrain()
    {
        var hex = new Hex(new HexCoordinates(1, 1));
        hex.AddTerrain(new BridgeTerrain());

        var result = hex.IsRoadOrPaved();

        result.ShouldBeTrue();
    }

    [Fact]
    public void IsRoadOrPaved_ReturnsFalse_ForClearTerrain()
    {
        var hex = new Hex(new HexCoordinates(1, 1));
        hex.AddTerrain(new ClearTerrain());

        var result = hex.IsRoadOrPaved();

        result.ShouldBeFalse();
    }

    [Fact]
    public void IsRoadOrPaved_ReturnsFalse_ForLightWoodsTerrain()
    {
        var hex = new Hex(new HexCoordinates(1, 1));
        hex.AddTerrain(new LightWoodsTerrain());

        var result = hex.IsRoadOrPaved();

        result.ShouldBeFalse();
    }
}
