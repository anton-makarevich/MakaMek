using Shouldly;
using Sanet.MakaMek.Core.Exceptions;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Utils.Generators;

namespace Sanet.MakaMek.Core.Tests.Utils.Generators;

public class AlternatingRowsGeneratorTests
{
    [Fact]
    public void GeneratesAlternatingTerrains()
    {
        // Arrange
        const int width = 5;
        const int height = 5;
        var evenTerrain = new ClearTerrain();
        var oddTerrain = new LightWoodsTerrain();
        var generator = new AlternatingRowsGenerator(width, height, evenTerrain, oddTerrain);

        // Act & Assert
        for (var r = 1; r < height+1; r++)
        {
            var hex = generator.Generate(new HexCoordinates(2, r));
            if (r % 2 == 0)
            {
                hex.HasTerrain(MakaMekTerrains.Clear).ShouldBeTrue();
                hex.HasTerrain((MakaMekTerrains.LightWoods)).ShouldBeFalse();
            }
            else
            {
                hex.HasTerrain(MakaMekTerrains.Clear).ShouldBeFalse();
                hex.HasTerrain((MakaMekTerrains.LightWoods)).ShouldBeTrue();
            }
        }
    }

    [Fact]
    public void OutOfBounds_ShouldThrow()
    {
        // Arrange
        const int width = 5;
        const int height = 5;
        var evenTerrain = new ClearTerrain();
        var oddTerrain = new LightWoodsTerrain();
        var generator = new AlternatingRowsGenerator(width, height, evenTerrain, oddTerrain);
        var coordinates = new HexCoordinates(width + 1, height + 1);
        
        // Act & Assert
        var ex = Should.Throw<HexOutsideOfMapBoundariesException>(() => generator.Generate(coordinates));
        ex.Coordinates.ShouldBe(coordinates);
        ex.MapWidth.ShouldBe(width);
        ex.MapHeight.ShouldBe(height);
    }

    [Fact]
    public void GeneratesConsistentTerrainForSameRow()
    {
        // Arrange
        const int width = 5;
        const int height = 5;
        var evenTerrain = new ClearTerrain();
        var oddTerrain = new LightWoodsTerrain();
        var generator = new AlternatingRowsGenerator(width, height, evenTerrain, oddTerrain);

        // Act & Assert
        for (var r = 1; r < height+1; r++)
        {
            var expectedTerrain = r % 2 == 0 ? MakaMekTerrains.Clear : MakaMekTerrains.LightWoods;

            // Check all hexes in the same row have the same terrain
            for (var q = 1; q < width+1; q++)
            {
                var hex = generator.Generate(new HexCoordinates(q, r));
                hex.HasTerrain(expectedTerrain).ShouldBeTrue();
            }
        }
    }
}
