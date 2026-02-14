using Sanet.MakaMek.Map.Exceptions;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Generators;

public class SingleTerrainGeneratorTests
{
    [Fact]
    public void GeneratesCorrectTerrain()
    {
        // Arrange
        const int width = 5;
        const int height = 5;
        var terrain = new ClearTerrain();
        var generator = new SingleTerrainGenerator(width, height, terrain);

        // Act
        var hex = generator.Generate(new HexCoordinates(2, 2));

        // Assert
        hex.HasTerrain(MakaMekTerrains.Clear).ShouldBeTrue();
    }

    [Theory]
    [InlineData(-1, 0)]  // Left of the map
    [InlineData(6, 0)]   // Right of the map
    [InlineData(0, -1)]  // Above the map
    [InlineData(0, 6)]   // Below the map
    public void OutOfBounds_ThrowsException(int q, int r)
    {
        // Arrange
        const int width = 5;
        const int height = 5;
        var terrain = new ClearTerrain();
        var generator = new SingleTerrainGenerator(width, height, terrain);
        var coordinates = new HexCoordinates(q, r);

        // Act & Assert
        var ex = Should.Throw<HexOutsideOfMapBoundariesException>(() => generator.Generate(coordinates));
        ex.Coordinates.ShouldBe(coordinates);
        ex.MapWidth.ShouldBe(width);
        ex.MapHeight.ShouldBe(height);
    }
}
