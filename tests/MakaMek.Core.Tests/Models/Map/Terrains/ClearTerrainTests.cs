using Shouldly;
using Sanet.MakaMek.Core.Models.Map.Terrains;

namespace Sanet.MakaMek.Core.Tests.Models.Map.Terrains;

public class ClearTerrainTests
{
    [Fact]
    public void Height_Returns0()
    {
        // Arrange
        var terrain = new ClearTerrain();

        // Act & Assert
        terrain.Height.ShouldBe(0);
    }

    [Fact]
    public void TerrainFactor_Returns1()
    {
        // Arrange
        var terrain = new ClearTerrain();

        // Act & Assert
        terrain.MovementCost.ShouldBe(1);
    }

    [Fact]
    public void Id_ReturnsClear()
    {
        // Arrange
        var terrain = new ClearTerrain();

        // Act & Assert
        terrain.Id.ShouldBe("Clear");
    }

    [Fact]
    public void InterveningFactor_IsZero()
    {
        // Arrange
        var terrain = new ClearTerrain();

        // Act & Assert
        terrain.InterveningFactor.ShouldBe(0);
    }
}
