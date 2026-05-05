using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Map;

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
}
