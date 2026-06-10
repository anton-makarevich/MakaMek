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
    public void MovementCost_Returns1()
    {
        // Arrange
        var terrain = new RoughTerrain();

        // Act & Assert
        terrain.MovementCost.ShouldBe(1);
    }

    [Fact]
    public void GetTerrainType_WithRough_ReturnsRoughTerrain()
    {
        // Act
        var terrain = Terrain.CreateTerrainOfType(MakaMekTerrains.Rough);

        // Assert
        terrain.ShouldBeOfType<RoughTerrain>();
    }
}
