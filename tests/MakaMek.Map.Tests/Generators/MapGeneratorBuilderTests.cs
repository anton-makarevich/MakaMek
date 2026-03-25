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
            .Select(c => sut.Generate(c))
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
            .Select(c => sut.Generate(c))
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

        var hexes = AllCoords().Select(c => sut.Generate(c)).ToList();

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
}

