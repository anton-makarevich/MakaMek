using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Generators;

public class RoadPathGeneratorTests
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
    public void GenerateRoads_WithPositiveCount_ReturnsNonEmptyRoadHexes()
    {
        var roadCount = 2;
        var sut = new MapGeneratorBuilder(Width, Height)
            .WithBaseTerrain(new ClearTerrain())
            .WithRoads(roadCount)
            .WithSeed(42)
            .Build();

        var roadHexes = AllCoords()
            .Select(sut.Generate)
            .Where(h => h.HasTerrain(MakaMekTerrains.Road) || h.HasTerrain(MakaMekTerrains.Bridge))
            .ToList();

        roadHexes.ShouldNotBeEmpty();
    }

    [Fact]
    public void GenerateRoads_WithZeroCount_ReturnsEmptyDictionary()
    {
        var generator = new RoadPathGenerator(Width, Height);

        var result = generator.GenerateRoads(0);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void GenerateRoads_NegativeCount_ThrowsArgumentOutOfRangeException()
    {
        var generator = new RoadPathGenerator(Width, Height);

        var ex = Should.Throw<ArgumentOutOfRangeException>(() => generator.GenerateRoads(-1));
        ex.ParamName.ShouldBe("roadCount");
    }

    [Fact]
    public void Constructor_WidthLessThan1_ThrowsArgumentOutOfRangeException()
    {
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => new RoadPathGenerator(0, Height));
        ex.ParamName.ShouldBe("width");
    }

    [Fact]
    public void Constructor_HeightLessThan1_ThrowsArgumentOutOfRangeException()
    {
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => new RoadPathGenerator(Width, 0));
        ex.ParamName.ShouldBe("height");
    }

    [Fact]
    public void GenerateRoads_SeededGeneration_IsReproducible()
    {
        var gen1 = new MapGeneratorBuilder(Width, Height)
            .WithBaseTerrain(new ClearTerrain())
            .WithRoads(3)
            .WithSeed(123)
            .Build();

        var gen2 = new MapGeneratorBuilder(Width, Height)
            .WithBaseTerrain(new ClearTerrain())
            .WithRoads(3)
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
    public void AtLeastOneRoadHexOnBoundary()
    {
        var sut = new MapGeneratorBuilder(Width, Height)
            .WithBaseTerrain(new ClearTerrain())
            .WithRoads(3)
            .WithSeed(42)
            .Build();

        var roadHexes = AllCoords()
            .Select(sut.Generate)
            .Where(h => h.HasTerrain(MakaMekTerrains.Road) || h.HasTerrain(MakaMekTerrains.Bridge))
            .Select(h => h.Coordinates)
            .ToHashSet();

        roadHexes.ShouldNotBeEmpty();

        var hasEdgeHex = roadHexes.Any(h =>
            h.Q == 1 || h.Q == Width || h.R == 1 || h.R == Height);

        hasEdgeHex.ShouldBeTrue();
    }

    [Fact]
    public void RoadHexesAreConnected()
    {
        var sut = new MapGeneratorBuilder(Width, Height)
            .WithBaseTerrain(new ClearTerrain())
            .WithRoads(3)
            .WithSeed(42)
            .Build();

        var roadHexes = AllCoords()
            .Select(sut.Generate)
            .Where(h => h.HasTerrain(MakaMekTerrains.Road) || h.HasTerrain(MakaMekTerrains.Bridge))
            .Select(h => h.Coordinates)
            .ToHashSet();

        roadHexes.ShouldNotBeEmpty();

        foreach (var hex in roadHexes)
        {
            var hasRoadNeighbor = hex.GetAllNeighbours().Any(n => roadHexes.Contains(n));
            hasRoadNeighbor.ShouldBeTrue();
        }
    }

    [Fact]
    public void PickRandomEdgeStart_DoesNotReturnWaterHex_WhenOnlyOneEdgeHexIsLand()
    {
        const int width = 3;
        const int height = 3;

        // All edge hexes are water except (1,1)
        var waterHexes = new HashSet<HexCoordinates>
        {
            new(1, 2), new(1, 3),
            new(2, 1), new(2, 3),
            new(3, 1), new(3, 2), new(3, 3)
        };

        var generator = new RoadPathGenerator(width, height, new Random(42), waterHexes);
        var roads = generator.GenerateRoads(1);

        // (1,1) is the only eligible edge start
        roads.Keys.First().ShouldBe(new HexCoordinates(1,1));
    }

    [Fact]
    public void RoadNetworkBranches()
    {
        var generator = new RoadPathGenerator(35, 35, new Random(42));
        var roads = generator.GenerateRoads(5);

        var hasBranchPoint = roads.Keys.Any(hex =>
            hex.GetAllNeighbours().Count(n => roads.ContainsKey(n)) >= 3);

        hasBranchPoint.ShouldBeTrue();
    }

    [Fact]
    public void NoDuplicateHexesInRoadSet()
    {
        var generator = new RoadPathGenerator(Width, Height, new Random(42));
        var roads = generator.GenerateRoads(3);
        var allHexes = roads.Keys.ToList();
        allHexes.Count.ShouldBe(allHexes.Distinct().Count());
    }
}
