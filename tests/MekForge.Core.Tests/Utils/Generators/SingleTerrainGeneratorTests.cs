using FluentAssertions;
using Sanet.MekForge.Core.Utils.Generators;
using Sanet.MekForge.Core.Exceptions;
using Sanet.MekForge.Core.Models.Map;
using Sanet.MekForge.Core.Models.Map.Terrains;

namespace Sanet.MekForge.Core.Tests.Utils.Generators;

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
        hex.HasTerrain("Clear").Should().BeTrue();
    }

    [Theory]
    [InlineData(-1, 0)]  // Left of map
    [InlineData(6, 0)]   // Right of map
    [InlineData(0, -1)]  // Above map
    [InlineData(0, 6)]   // Below map
    public void OutOfBounds_ThrowsException(int q, int r)
    {
        // Arrange
        const int width = 5;
        const int height = 5;
        var terrain = new ClearTerrain();
        var generator = new SingleTerrainGenerator(width, height, terrain);
        var coordinates = new HexCoordinates(q, r);

        // Act & Assert
        var action = () => generator.Generate(coordinates);
        action.Should().Throw<HexOutsideOfMapBoundariesException>()
            .Which.Should().Match<HexOutsideOfMapBoundariesException>(ex =>
                ex.Coordinates == coordinates &&
                ex.MapWidth == width &&
                ex.MapHeight == height);
    }
}
