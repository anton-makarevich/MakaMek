using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Terrains;

public class WaterTerrainTests
{
    [Fact]
    public void Id_ReturnsWater()
    {
        // Arrange
        var terrain = new WaterTerrain();

        // Act & Assert
        terrain.Id.ShouldBe(MakaMekTerrains.Water);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, -1)]
    [InlineData(-2, -2)]
    [InlineData(-3, -3)]
    public void Height_ReturnsDepthValue(int depth, int expectedHeight)
    {
        // Arrange
        var terrain = new WaterTerrain(depth);

        // Act & Assert
        terrain.Height.ShouldBe(expectedHeight);
    }

    [Fact]
    public void InterveningFactor_IsZero()
    {
        // Arrange
        var terrain = new WaterTerrain();

        // Act & Assert
        terrain.InterveningFactor.ShouldBe(0);
    }

    [Fact]
    public void MovementCost_ShallowWater_Returns1()
    {
        // Arrange
        var terrain = new WaterTerrain();

        // Act & Assert
        terrain.MovementCost.ShouldBe(1);
    }

    [Fact]
    public void MovementCost_StandardDepth_Returns2()
    {
        // Arrange
        var terrain = new WaterTerrain(-1);

        // Act & Assert
        terrain.MovementCost.ShouldBe(2);
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(-3)]
    [InlineData(-5)]
    public void MovementCost_DeepWater_Returns4(int depth)
    {
        // Arrange
        var terrain = new WaterTerrain(depth);

        // Act & Assert
        terrain.MovementCost.ShouldBe(4);
    }
}
