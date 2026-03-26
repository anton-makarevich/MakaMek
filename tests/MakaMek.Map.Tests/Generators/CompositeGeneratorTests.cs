using NSubstitute;
using Sanet.MakaMek.Map.Exceptions;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Generators.Levels;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Generators;

public class CompositeGeneratorTests
{
    [Theory]
    [InlineData(0, 1)]   // Q < 1 (left of map)
    [InlineData(6, 1)]   // Q >= width + 1 (right of map)
    [InlineData(1, 0)]   // R < 1 (above map)
    [InlineData(1, 6)]   // R >= height + 1 (below map)
    public void Generate_WithOutOfBoundsCoordinates_ThrowsHexOutsideOfMapBoundariesException(int q, int r)
    {
        // Arrange
        const int width = 5;
        const int height = 5;
        var baseTerrain = new ClearTerrain();
        var levelProvider = Substitute.For<ILevelProvider>();
        var overlays = new List<(HashSet<HexCoordinates>, Func<HexCoordinates, Random, Terrain>)>();
        var random = new Random(42);
        var sut = new CompositeGenerator(width, height, baseTerrain, levelProvider, overlays, random);
        var coordinates = new HexCoordinates(q, r);

        // Act & Assert
        var ex = Should.Throw<HexOutsideOfMapBoundariesException>(() => sut.Generate(coordinates));
        ex.Coordinates.ShouldBe(coordinates);
        ex.MapWidth.ShouldBe(width);
        ex.MapHeight.ShouldBe(height);
    }
}
