using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Terrains;

public class LightWoodsTerrainTests
{
    [Fact]
    public void Height_Returns_2()
    {
        // Arrange
        var terrain = new LightWoodsTerrain();

        // Act & Assert
        terrain.Height.ShouldBe(2);
    }

    [Fact]
    public void TerrainFactor_Returns2()
    {
        // Arrange
        var terrain = new LightWoodsTerrain();

        // Act & Assert
        terrain.MovementCost.ShouldBe(2);
    }

    [Fact]
    public void Id_ReturnsLightWoods()
    {
        // Arrange
        var terrain = new LightWoodsTerrain();

        // Act & Assert
        terrain.Id.ShouldBe(MakaMekTerrains.LightWoods);
    }

    [Fact]
    public void InterveningFactor_IsOne()
    {
        // Arrange
        var terrain = new LightWoodsTerrain();

        // Act & Assert
        terrain.InterveningFactor.ShouldBe(1);
    }
}
