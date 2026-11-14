using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Generators;

/// <summary>
/// Locks the seeded generator output so the shared <see cref="PathWalker"/> extraction stays
/// behaviour-preserving: the random draw order of every generator must not change.
/// </summary>
public class GeneratorGoldenTests
{
    [Fact]
    public void GenerateRivers_Seeded_ProducesTheGoldenNetwork()
    {
        var sut = new RiverPathGenerator(8, 6, new Random(12345));

        var rivers = sut.GenerateRivers(3);

        Normalise(rivers).ShouldBe(
        [
            (1, 3, 0), (2, 1, 0), (2, 3, 0), (3, 2, 0), (3, 4, 0), (3, 5, 0), (3, 6, 0),
            (4, 2, 0), (5, 2, 0), (6, 1, 0), (6, 2, 0), (7, 1, 0), (7, 2, 0)
        ]);
    }

    [Fact]
    public void GenerateRivers_WithLevelLookup_ProducesTheGoldenNetwork()
    {
        var sut = new RiverPathGenerator(8, 6, new Random(777), null, c => (c.Q + c.R) % 4);

        var rivers = sut.GenerateRivers(2);

        Normalise(rivers).ShouldBe([(2, 5, 0), (2, 6, 0), (6, 6, 0), (7, 6, 0), (8, 6, 0)]);
    }

    [Fact]
    public void GenerateRoads_Seeded_ProducesTheGoldenNetwork()
    {
        var sut = new RoadPathGenerator(8, 6, new Random(12345));

        var roads = sut.GenerateRoads(2);

        Normalise(roads).ShouldBe(
        [
            (2, 1, 0), (3, 1, 0), (3, 2, 0), (4, 2, 0), (5, 3, 0), (5, 4, 0), (5, 5, 0), (5, 6, 0)
        ]);
    }

    [Fact]
    public void GenerateRoads_WithWaterAndLevelLookup_ProducesTheGoldenNetwork()
    {
        var water = new HashSet<HexCoordinates> { new(1, 1), new(1, 2) };
        var sut = new RoadPathGenerator(8, 6, new Random(4242), water, c => (c.Q * 3 + c.R) % 3);

        var roads = sut.GenerateRoads(3);

        Normalise(roads).ShouldBe(
        [
            (3, 3, 0), (4, 3, 0), (5, 4, 0), (5, 5, 0), (6, 3, 0),
            (6, 4, 0), (7, 3, 0), (7, 5, 0), (8, 2, 0), (8, 5, 0)
        ]);
    }

    [Fact]
    public void GeneratePatches_Seeded_ProducesTheGoldenDistances()
    {
        var sut = new PatchGenerator(10, 8, new Random(99));

        var patches = sut.GeneratePatches(0.3);

        Normalise(patches).ShouldBe(
        [
            (0, 7, 0), (1, 1, 0), (1, 2, 1), (1, 6, 2), (1, 7, 1), (1, 8, 1), (2, 1, 1),
            (3, 1, 2), (4, 1, 0), (4, 5, 1), (4, 6, 0), (4, 7, 1), (5, 1, 1), (5, 7, 1),
            (6, 2, 1), (6, 3, 0), (6, 6, 1), (6, 7, 0), (8, 1, 1), (8, 2, 0), (9, 1, 1),
            (9, 2, 0), (10, 1, 1), (10, 2, 1)
        ]);
    }

    [Fact]
    public void GeneratePatches_RespectsMaxPatchSize()
    {
        var patches = new PatchGenerator(10, 8, new Random(7)).GeneratePatches(0.5, 3, 6);

        patches.Count.ShouldBe(41);
        patches.Values.ShouldAllBe(distance => distance <= 6);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(12345)]
    public void SeededGeneration_IsReproducible(int seed)
    {
        var first = Normalise(new PatchGenerator(10, 8, new Random(seed)).GeneratePatches(0.3));
        var second = Normalise(new PatchGenerator(10, 8, new Random(seed)).GeneratePatches(0.3));

        second.ShouldBe(first);
    }

    private static (int Q, int R, int Value)[] Normalise(Dictionary<HexCoordinates, int> map)
    {
        return map
            .OrderBy(pair => pair.Key.Q)
            .ThenBy(pair => pair.Key.R)
            .Select(pair => (pair.Key.Q, pair.Key.R, pair.Value))
            .ToArray();
    }
}
