using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Generators;

public class RiverPathGeneratorTests
{
    private const int Width = 15;
    private const int Height = 15;

    private static IEnumerable<HexCoordinates> AllCoords()
    {
        for (var q = 1; q <= Width; q++)
        for (var r = 1; r <= Height; r++)
            yield return new HexCoordinates(q, r);
    }

    [Fact]
    public void GenerateRivers_WithPositiveCount_ReturnsRiverHexes()
    {
        var riverCount = 3;
        var sut = new MapGeneratorBuilder(Width, Height)
            .WithBaseTerrain(new ClearTerrain())
            .WithRivers(riverCount)
            .WithSeed(42)
            .Build();

        var riverHexes = AllCoords()
            .Select(sut.Generate)
            .Where(h => h.HasTerrain(MakaMekTerrains.Water))
            .ToList();

        riverHexes.ShouldNotBeEmpty();
    }

    [Fact]
    public void GenerateRivers_WithZeroCount_ReturnsNoRiverHexes()
    {
        var sut = new MapGeneratorBuilder(Width, Height)
            .WithBaseTerrain(new ClearTerrain())
            .WithRivers(0)
            .WithSeed(42)
            .Build();

        var riverHexes = AllCoords()
            .Select(sut.Generate)
            .Where(h => h.HasTerrain(MakaMekTerrains.Water))
            .ToList();

        riverHexes.ShouldBeEmpty();
    }

    [Fact]
    public void GenerateRivers_RiversFormConnectedChains()
    {
        var sut = new MapGeneratorBuilder(Width, Height)
            .WithBaseTerrain(new ClearTerrain())
            .WithRivers(3)
            .WithSeed(42)
            .Build();

        var riverHexes = AllCoords()
            .Select(sut.Generate)
            .Where(h => h.HasTerrain(MakaMekTerrains.Water))
            .Select(h => h.Coordinates)
            .ToHashSet();

        riverHexes.ShouldNotBeEmpty();

        foreach (var hex in riverHexes)
        {
            var hasRiverNeighbor = hex.GetAllNeighbours().Any(n => riverHexes.Contains(n));
            hasRiverNeighbor.ShouldBeTrue();
        }
    }

    [Fact]
    public void GenerateRivers_RiversStartAtEdge()
    {
        var sut = new MapGeneratorBuilder(Width, Height)
            .WithBaseTerrain(new ClearTerrain())
            .WithRivers(5)
            .WithSeed(42)
            .Build();

        var riverHexes = AllCoords()
            .Select(sut.Generate)
            .Where(h => h.HasTerrain(MakaMekTerrains.Water))
            .Select(h => h.Coordinates)
            .ToHashSet();

        riverHexes.ShouldNotBeEmpty();

        var hasEdgeHex = riverHexes.Any(h =>
            h.Q == 1 || h.Q == Width || h.R == 1 || h.R == Height);

        hasEdgeHex.ShouldBeTrue();
    }

    [Fact]
    public void CombiningRiversAndLakes_ProducesWater()
    {
        var sut = new MapGeneratorBuilder(Width, Height)
            .WithBaseTerrain(new ClearTerrain())
            .WithLakes(coverage: 0.3, maxDepth: 2)
            .WithRivers(3)
            .WithSeed(42)
            .Build();

        var hexes = AllCoords().Select(sut.Generate).ToList();

        hexes.ShouldContain(h => h.HasTerrain(MakaMekTerrains.Water));
    }

    [Fact]
    public void WithRivers_SeededGeneration_IsReproducible()
    {
        var gen1 = new MapGeneratorBuilder(Width, Height)
            .WithBaseTerrain(new ClearTerrain())
            .WithRivers(3)
            .WithSeed(123)
            .Build();

        var gen2 = new MapGeneratorBuilder(Width, Height)
            .WithBaseTerrain(new ClearTerrain())
            .WithRivers(3)
            .WithSeed(123)
            .Build();

        foreach (var coords in AllCoords())
        {
            var h1 = gen1.Generate(coords);
            var h2 = gen2.Generate(coords);
            h1.GetTerrains().Select(t => t.ToData().Type)
                .ShouldBe(h2.GetTerrains().Select(t => t.ToData().Type));
        }
    }

    [Fact]
    public void GenerateRivers_NegativeCount_ThrowsArgumentOutOfRangeException()
    {
        var generator = new RiverPathGenerator(Width, Height);

        var ex = Should.Throw<ArgumentOutOfRangeException>(() => generator.GenerateRivers(-1));
        ex.ParamName.ShouldBe("riverCount");
    }

    [Fact]
    public void Constructor_WidthLessThan1_ThrowsArgumentOutOfRangeException()
    {
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => new RiverPathGenerator(0, Height));
        ex.ParamName.ShouldBe("width");
    }

    [Fact]
    public void Constructor_HeightLessThan1_ThrowsArgumentOutOfRangeException()
    {
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => new RiverPathGenerator(Width, 0));
        ex.ParamName.ShouldBe("height");
    }

    [Fact]
    public void GenerateSingleRiver_SecondHexIsCloserToCenterThanFirst()
    {
        var generator = new RiverPathGenerator(Width, Height, new Random(42));
        var river = generator.GenerateSingleRiver(new Dictionary<HexCoordinates, int>());

        river.Count.ShouldBeGreaterThanOrEqualTo(2);

        var center = new HexCoordinates((Width + 1) / 2, (Height + 1) / 2);
        var firstDist = river[0].DistanceTo(center);
        var secondDist = river[1].DistanceTo(center);

        secondDist.ShouldBeLessThanOrEqualTo(firstDist);
    }

    [Fact]
    public void GenerateSingleRiver_NoDuplicateHexes()
    {
        const int seed = 43; // Produces looping river

        var generator = new RiverPathGenerator(35, 35, new Random(seed));
        var river = generator.GenerateSingleRiver(new Dictionary<HexCoordinates, int>());
        river.Count.ShouldBe(river.Distinct().Count());
    }

    [Fact]
    public void GenerateRivers_WithElevationGradient_FlowsSingleDirection()
    {
        // Level = Q creates a gradient: low on the left, high on the right.
        // Every river's non-zero elevation deltas must share a single sign.
        Func<HexCoordinates, int> levelLookup = coords => coords.Q - 1;

        var generator = new RiverPathGenerator(
            Width, Height, new Random(42), levelLookup: levelLookup);

        var rivers = Enumerable.Range(0, 5)
            .Select(_ => generator.GenerateSingleRiver(new Dictionary<HexCoordinates, int>()))
            .ToList();

        foreach (var river in rivers)
        {
            int? direction = null;
            for (var i = 1; i < river.Count; i++)
            {
                var delta = levelLookup(river[i]) - levelLookup(river[i - 1]);
                if (delta == 0)
                    continue;
                var sign = Math.Sign(delta);
                direction ??= sign;
                sign.ShouldBe(direction.Value, "river must not reverse vertical direction");
            }
        }
    }

    [Fact]
    public void GenerateRivers_WithStepElevation_TerminatesAtLevelChangeReversal()
    {
        // Left half level 0, right half level 5 — a sharp cliff at Q=8.
        // Rivers starting on one side will lock flow direction and stop
        // when they'd need to reverse.
        Func<HexCoordinates, int> levelLookup = coords => coords.Q >= 8 ? 5 : 0;

        var generator = new RiverPathGenerator(
            Width, Height, new Random(19), levelLookup: levelLookup);

        var river = generator.GenerateSingleRiver(new Dictionary<HexCoordinates, int>());

        var direction = 0;
        for (var i = 1; i < river.Count; i++)
        {
            var delta = levelLookup(river[i]) - levelLookup(river[i - 1]);
            if (delta == 0)
                continue;
            if (direction == 0)
                direction = Math.Sign(delta);
            else
            {
                // If the river ever reversed direction it would have been
                // terminated; therefore every non-zero delta must match.
                Math.Sign(delta).ShouldBe(direction);
            }
        }

        // The river should still produce at least a start hex
        river.ShouldNotBeEmpty();

        // Verify the test was non-vacuous: the river must have crossed the
        // elevation step at least once, otherwise direction locking was never
        // exercised.
        direction.ShouldNotBe(0, "river must cross the Q=8 cliff to test reversal");
    }

    [Fact]
    public void GenerateRivers_WithoutElevation_BehavesAsBefore()
    {
        var withElevation = new RiverPathGenerator(
            Width, Height, new Random(42),
            levelLookup: coords => coords.Q);

        var withoutElevation = new RiverPathGenerator(
            Width, Height, new Random(42));

        var riverWith = withElevation.GenerateSingleRiver(new Dictionary<HexCoordinates, int>());
        var riverWithout = withoutElevation.GenerateSingleRiver(new Dictionary<HexCoordinates, int>());

        // Both should produce valid rivers starting at the edge
        riverWith.ShouldNotBeEmpty();
        riverWithout.ShouldNotBeEmpty();

        // The paths may differ because elevation constrains flow,
        // but both must produce connected paths (each hex has a neighbor)
        static bool HasNeighbor(HexCoordinates hex, List<HexCoordinates> river) =>
            river.Any(h => h != hex && hex.GetAllNeighbours().Contains(h));

        if (riverWithout.Count > 1)
            riverWithout.All(h => HasNeighbor(h, riverWithout)).ShouldBeTrue();

        if (riverWith.Count > 1)
            riverWith.All(h => HasNeighbor(h, riverWith)).ShouldBeTrue();
    }

    [Fact]
    public void GenerateRivers_WithElevation_SeededGeneration_IsReproducible()
    {
        Func<HexCoordinates, int> levelLookup = coords => coords.Q;

        RiverPathGenerator CreateGenerator(int seed) => new(
            Width, Height, new Random(seed), levelLookup: levelLookup);

        var river1 = CreateGenerator(99).GenerateSingleRiver(new Dictionary<HexCoordinates, int>());
        var river2 = CreateGenerator(99).GenerateSingleRiver(new Dictionary<HexCoordinates, int>());

        river1.ShouldBe(river2);
    }
}
