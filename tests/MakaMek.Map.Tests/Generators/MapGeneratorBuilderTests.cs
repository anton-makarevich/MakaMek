using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Generators;

public class MapGeneratorBuilderTests
{
    private const int Width = 10;
    private const int Height = 10;

    private static IEnumerable<HexCoordinates> AllCoords()
    {
        for (var q = 1; q <= Width; q++)
        for (var r = 1; r <= Height; r++)
            yield return new HexCoordinates(q, r);
    }

    [Fact]
    public void BaseTerrainOnly_ProducesExpectedTerrain()
    {
        var sut = new MapGeneratorBuilder(Width, Height)
            .WithBaseTerrain(new ClearTerrain())
            .Build();

        foreach (var coords in AllCoords())
        {
            var hex = sut.Generate(coords);
            hex.HasTerrain(MakaMekTerrains.Clear).ShouldBeTrue();
        }
    }

    [Fact]
    public void WithForestPatches_ProducesForestHexes()
    {
        var sut = new MapGeneratorBuilder(Width, Height)
            .WithBaseTerrain(new ClearTerrain())
            .WithForestPatches(coverage: 0.5, lightWoodsProbability: 1.0)
            .WithSeed(42)
            .Build();

        var hasForest = AllCoords()
            .Select(sut.Generate)
            .Any(h => h.HasTerrain(MakaMekTerrains.LightWoods) || h.HasTerrain(MakaMekTerrains.HeavyWoods));

        hasForest.ShouldBeTrue();
    }

    [Fact]
    public void WithHills_ProducesNonZeroLevels()
    {
        var sut = new MapGeneratorBuilder(Width, Height)
            .WithHills(coverage: 0.4, maxElevation: 3)
            .WithSeed(42)
            .Build();

        var hasElevation = AllCoords()
            .Select(sut.Generate)
            .Any(h => h.Level > 0);

        hasElevation.ShouldBeTrue();
    }

    [Fact]
    public void CombiningTerrainAndHills_SetsBothCorrectly()
    {
        var sut = new MapGeneratorBuilder(Width, Height)
            .WithBaseTerrain(new ClearTerrain())
            .WithForestPatches(coverage: 0.4, lightWoodsProbability: 1.0)
            .WithHills(coverage: 0.4, maxElevation: 3)
            .WithSeed(99)
            .Build();

        var hexes = AllCoords().Select(sut.Generate).ToList();

        hexes.ShouldContain(h => h.HasTerrain(MakaMekTerrains.LightWoods));
        hexes.ShouldContain(h => h.Level > 0);
    }

    [Fact]
    public void FluentChaining_WorksWithoutSideEffects()
    {
        // Building twice from the same builder should not fail
        var builder = new MapGeneratorBuilder(Width, Height)
            .WithBaseTerrain(new ClearTerrain())
            .WithForestPatches(coverage: 0.3, lightWoodsProbability: 0.6)
            .WithHills(coverage: 0.3, maxElevation: 2)
            .WithSeed(7);

        var gen1 = builder.Build();
        var gen2 = builder.Build();

        // Both generators should produce valid hexes for all coordinates
        foreach (var coords in AllCoords())
        {
            var h1 = gen1.Generate(coords);
            var h2 = gen2.Generate(coords);
            h1.ShouldNotBeNull();
            h2.ShouldNotBeNull();
        }
    }

    [Fact]
    public void WithSeed_ProducesReproducibleResults()
    {
        var gen1 = new MapGeneratorBuilder(Width, Height)
            .WithForestPatches(coverage: 0.3, lightWoodsProbability: 0.6)
            .WithHills(coverage: 0.3, maxElevation: 3)
            .WithSeed(123)
            .Build();

        var gen2 = new MapGeneratorBuilder(Width, Height)
            .WithForestPatches(coverage: 0.3, lightWoodsProbability: 0.6)
            .WithHills(coverage: 0.3, maxElevation: 3)
            .WithSeed(123)
            .Build();

        foreach (var coords in AllCoords())
        {
            var h1 = gen1.Generate(coords);
            var h2 = gen2.Generate(coords);
            h1.Level.ShouldBe(h2.Level);
            h1.GetTerrainTypes().ShouldBe(h2.GetTerrainTypes());
        }
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void WithTerrain_WithInvalidCoverage_ThrowsArgumentOutOfRangeException(double coverage)
    {
        // Arrange
        var builder = new MapGeneratorBuilder(Width, Height);

        // Act & Assert
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => builder.WithTerrain<ClearTerrain>(coverage));
        ex.ParamName.ShouldBe("coverage");
    }

    [Fact]
    public void WithTerrain_WithValidCoverage_ProducesTerrain()
    {
        // Arrange & Act
        var sut = new MapGeneratorBuilder(Width, Height)
            .WithTerrain<LightWoodsTerrain>(coverage: 0.5)
            .WithSeed(42)
            .Build();

        // Assert
        var hasTerrain = AllCoords()
            .Select(sut.Generate)
            .Any(h => h.HasTerrain(MakaMekTerrains.LightWoods));

        hasTerrain.ShouldBeTrue();
    }

    [Theory]
    [InlineData(-0.1, 0.5)]
    [InlineData(1.1, 0.5)]
    public void WithForestPatches_WithInvalidCoverage_ThrowsArgumentOutOfRangeException(double coverage, double lightWoodsProbability)
    {
        // Arrange
        var builder = new MapGeneratorBuilder(Width, Height);

        // Act & Assert
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => builder.WithForestPatches(coverage, lightWoodsProbability));
        ex.ParamName.ShouldBe("coverage");
    }

    [Theory]
    [InlineData(0.5, -0.1)]
    [InlineData(0.5, 1.1)]
    public void WithForestPatches_WithInvalidLightWoodsProbability_ThrowsArgumentOutOfRangeException(double coverage, double lightWoodsProbability)
    {
        // Arrange
        var builder = new MapGeneratorBuilder(Width, Height);

        // Act & Assert
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => builder.WithForestPatches(coverage, lightWoodsProbability));
        ex.ParamName.ShouldBe("lightWoodsProbability");
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    public void Constructor_WithInvalidWidth_ThrowsArgumentOutOfRangeException(int width, int height)
    {
        // Act & Assert
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => new MapGeneratorBuilder(width, height));
        ex.ParamName.ShouldBe("width");
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(10, -1)]
    public void Constructor_WithInvalidHeight_ThrowsArgumentOutOfRangeException(int width, int height)
    {
        // Act & Assert
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => new MapGeneratorBuilder(width, height));
        ex.ParamName.ShouldBe("height");
    }

    [Fact]
    public void WithLakes_ProducesWaterHexes()
    {
        var sut = new MapGeneratorBuilder(Width, Height)
            .WithBaseTerrain(new ClearTerrain())
            .WithLakes(coverage: 0.5, maxDepth: 2)
            .WithSeed(42)
            .Build();

        var hasWater = AllCoords()
            .Select(sut.Generate)
            .Any(h => h.HasTerrain(MakaMekTerrains.Water));

        hasWater.ShouldBeTrue();
    }

    [Theory]
    [InlineData(-0.1, 2)]
    [InlineData(1.1, 2)]
    public void WithLakes_WithInvalidCoverage_ThrowsArgumentOutOfRangeException(double coverage, int maxDepth)
    {
        // Arrange
        var builder = new MapGeneratorBuilder(Width, Height);

        // Act & Assert
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => builder.WithLakes(coverage, maxDepth));
        ex.ParamName.ShouldBe("coverage");
    }

    [Theory]
    [InlineData(0.5, 0)]
    [InlineData(0.5, -1)]
    public void WithLakes_WithInvalidMaxDepth_ThrowsArgumentOutOfRangeException(double coverage, int maxDepth)
    {
        // Arrange
        var builder = new MapGeneratorBuilder(Width, Height);

        // Act & Assert
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => builder.WithLakes(coverage, maxDepth));
        ex.ParamName.ShouldBe("maxDepth");
    }

    [Fact]
    public void CombiningLakesAndHills_SetsBothCorrectly()
    {
        var sut = new MapGeneratorBuilder(Width, Height)
            .WithBaseTerrain(new ClearTerrain())
            .WithLakes(coverage: 0.4, maxDepth: 3)
            .WithHills(coverage: 0.4, maxElevation: 3)
            .WithSeed(99)
            .Build();

        var hexes = AllCoords().Select(sut.Generate).ToList();

        hexes.ShouldContain(h => h.HasTerrain(MakaMekTerrains.Water));
        hexes.ShouldContain(h => h.Level > 0);
    }
}