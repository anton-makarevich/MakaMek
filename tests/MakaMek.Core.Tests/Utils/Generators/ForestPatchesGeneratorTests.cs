using Shouldly;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MakaMek.Core.Exceptions;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;

namespace Sanet.MakaMek.Core.Tests.Utils.Generators;

public class ForestPatchesGeneratorTests
{
    [Fact]
    public void WithZeroForestCoverage_GeneratesOnlyClearTerrain()
    {
        // Arrange
        const int width = 10;
        const int height = 10;
        var generator = new ForestPatchesGenerator(width, height, forestCoverage: 0);

        // Act & Assert
        for (var q = 1; q < width+1; q++)
        {
            for (var r = 1; r < height+1; r++)
            {
                var hex = generator.Generate(new HexCoordinates(q, r));
                hex.HasTerrain(MakaMekTerrains.Clear).ShouldBeTrue();
                hex.HasTerrain(MakaMekTerrains.LightWoods).ShouldBeFalse();
                hex.HasTerrain(MakaMekTerrains.HeavyWoods).ShouldBeFalse();
            }
        }
    }

    [Fact]
    public void WithFullForestCoverage_GeneratesOnlyWoodsTerrain()
    {
        // Arrange
        const int width = 10;
        const int height = 10;
        var generator = new ForestPatchesGenerator(
            width, height,
            forestCoverage: 1.0,
            lightWoodsProbability: 1.0); // Force all woods to be light woods for deterministic test

        // Act & Assert
        var hasAnyWoods = false;
        for (var q = 1; q < width+1; q++)
        {
            for (var r = 1; r < height+1; r++)
            {
                var hex = generator.Generate(new HexCoordinates(q, r));
                hex.HasTerrain(MakaMekTerrains.Clear).ShouldBeFalse();
                if (hex.HasTerrain((MakaMekTerrains.LightWoods)) || hex.HasTerrain(MakaMekTerrains.HeavyWoods))
                {
                    hasAnyWoods = true;
                }
            }
        }
        hasAnyWoods.ShouldBeTrue();
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
        var generator = new ForestPatchesGenerator(width, height);
        var coordinates = new HexCoordinates(q, r);

        // Act & Assert
        var ex = Should.Throw<HexOutsideOfMapBoundariesException>(() => generator.Generate(coordinates));
        ex.Coordinates.ShouldBe(coordinates);
        ex.MapWidth.ShouldBe(width);
        ex.MapHeight.ShouldBe(height);
    }

    [Fact]
    public void CreatesPatches()
    {
        // Arrange
        const int width = 15;
        const int height = 15;
        var generator = new ForestPatchesGenerator(
            width, height,
            forestCoverage: 0.3,
            lightWoodsProbability: 0.5,
            minPatchSize: 3,
            maxPatchSize: 5);

        // Act
        var hexes = new List<Hex>();
        for (var q = 1; q < width+1; q++)
        {
            for (var r = 1; r < height+1; r++)
            {
                hexes.Add(generator.Generate(new HexCoordinates(q, r)));
            }
        }

        // Assert
        // Count hexes with woods
        var woodsHexes = hexes.Count(h => h.HasTerrain((MakaMekTerrains.LightWoods)) || h.HasTerrain(MakaMekTerrains.HeavyWoods));
        woodsHexes.ShouldBeGreaterThan(0);

        // Verify that woods appear in patches by checking for adjacent woods hexes
        var hasAdjacentWoods = false;
        for (var q = 1; q < width - 1; q++)
        {
            for (var r = 1; r < height - 1; r++)
            {
                var currentHex = hexes[q * height + r];
                if (currentHex.HasTerrain((MakaMekTerrains.LightWoods)) || currentHex.HasTerrain(MakaMekTerrains.HeavyWoods))
                {
                    // Check adjacent hexes
                    var coords = new HexCoordinates(q, r);
                    foreach (var neighbor in coords.GetAdjacentCoordinates())
                    {
                        if (neighbor.Q is >= 1 and < width+1 &&
                            neighbor.R is >= 1 and < height+1)
                        {
                            var neighborHex = hexes[neighbor.Q * (height+1) + neighbor.R];
                            if (neighborHex.HasTerrain((MakaMekTerrains.LightWoods)) || neighborHex.HasTerrain(MakaMekTerrains.HeavyWoods))
                            {
                                hasAdjacentWoods = true;
                                break;
                            }
                        }
                    }
                }
                if (hasAdjacentWoods) break;
            }
            if (hasAdjacentWoods) break;
        }
        hasAdjacentWoods.ShouldBeTrue();
    }
}
