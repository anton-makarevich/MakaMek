using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Map.Terrains;

public class HeavyWoodsTerrainTests
{
    [Fact]
    public void Height_Returns2()
    {
        // Arrange
        var terrain = new HeavyWoodsTerrain();

        // Act & Assert
        terrain.Height.ShouldBe(2);
    }

    [Fact]
    public void TerrainFactor_Returns3()
    {
        // Arrange
        var terrain = new HeavyWoodsTerrain();

        // Act & Assert
        terrain.MovementCost.ShouldBe(3);
    }

    [Fact]
    public void Id_ReturnsHeavyWoods()
    {
        // Arrange
        var terrain = new HeavyWoodsTerrain();

        // Act & Assert
        terrain.Id.ShouldBe(MakaMekTerrains.HeavyWoods);
    }

    [Fact]
    public void InterveningFactor_IsTwo()
    {
        // Arrange
        var terrain = new HeavyWoodsTerrain();

        // Act & Assert
        terrain.InterveningFactor.ShouldBe(2);
    }
}
