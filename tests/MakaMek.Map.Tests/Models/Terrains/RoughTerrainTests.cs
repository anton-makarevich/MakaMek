using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Terrains;

public class RoughTerrainTests
{
    [Fact]
    public void Id_ReturnsRough()
    {
        // Arrange
        var terrain = new RoughTerrain();

        // Act & Assert
        terrain.Id.ShouldBe(MakaMekTerrains.Rough);
    }

    [Fact]
    public void Height_Returns0()
    {
        // Arrange
        var terrain = new RoughTerrain();

        // Act & Assert
        terrain.Height.ShouldBe(0);
    }

    [Fact]
    public void InterveningFactor_Returns0()
    {
        // Arrange
        var terrain = new RoughTerrain();

        // Act & Assert
        terrain.InterveningFactor.ShouldBe(0);
    }

    [Fact]
    public void MovementCost_Returns2()
    {
        // Arrange
        var terrain = new RoughTerrain();

        // Act & Assert
        // Rough terrain costs 1 base MP + 1 additional MP = 2 MP total
        terrain.MovementCost.ShouldBe(2);
    }

    [Fact]
    public void GetTerrainType_WithRough_ReturnsRoughTerrain()
    {
        // Act
        var terrain = Terrain.GetTerrainType(MakaMekTerrains.Rough);

        // Assert
        terrain.ShouldBeOfType<RoughTerrain>();
    }
}
