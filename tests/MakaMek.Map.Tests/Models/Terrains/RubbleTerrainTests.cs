using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Terrains;

public class RubbleTerrainTests
{
    [Fact]
    public void Height_Returns0()
    {
        // Arrange
        var terrain = new RubbleTerrain();

        // Act & Assert
        terrain.Height.ShouldBe(0);
    }

    [Fact]
    public void Id_ReturnsRubble()
    {
        // Arrange
        var terrain = new RubbleTerrain();

        // Act & Assert
        terrain.Id.ShouldBe(MakaMekTerrains.Rubble);
    }

    [Fact]
    public void InterveningFactor_IsZero()
    {
        // Arrange
        var terrain = new RubbleTerrain();

        // Act & Assert
        terrain.InterveningFactor.ShouldBe(0);
    }
}
