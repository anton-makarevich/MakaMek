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
}
