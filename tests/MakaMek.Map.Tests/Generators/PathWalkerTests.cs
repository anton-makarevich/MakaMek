using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Generators;

public class PathWalkerTests
{
    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(3, 2, true)]
    [InlineData(0, 1, false)]
    [InlineData(4, 1, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 3, false)]
    public void IsOnMap_UsesOneBasedBounds(int q, int r, bool expected)
    {
        var sut = new PathWalker(3, 2);

        sut.IsOnMap(new HexCoordinates(q, r)).ShouldBe(expected);
    }

    [Fact]
    public void Flood_WithoutLimit_ReachesEveryHexOnTheMap()
    {
        var sut = new PathWalker(3, 3);

        var visited = sut.Flood(new HexCoordinates(2, 2), (_, _) => true);

        visited.Count.ShouldBe(9);
        visited.ShouldContain(new HexCoordinates(1, 1));
        visited.ShouldContain(new HexCoordinates(3, 3));
    }

    [Fact]
    public void Flood_RespectsMaxSize()
    {
        var sut = new PathWalker(4, 4);

        var visited = sut.Flood(new HexCoordinates(2, 2), (_, _) => true, maxSize: 3);

        visited.Count.ShouldBe(3);
        visited.ShouldContain(new HexCoordinates(2, 2));
    }

    [Fact]
    public void Flood_DoesNotExpandRejectedHexes()
    {
        var sut = new PathWalker(5, 5);
        var start = new HexCoordinates(1, 1);

        var visited = sut.Flood(start, (_, candidate) => candidate == start.GetNeighbour(HexDirection.BottomRight));

        visited.Count.ShouldBe(2);
    }

    [Fact]
    public void Flood_VisitsInBreadthFirstOrder()
    {
        var sut = new PathWalker(3, 3);
        var start = new HexCoordinates(2, 2);
        var accepted = new List<HexCoordinates>();

        sut.Flood(start, (_, candidate) =>
        {
            accepted.Add(candidate);
            return true;
        });

        // The start hex is seeded directly; the first candidates are its six neighbours in
        // neighbour order, so the flood is breadth-first.
        accepted.Count.ShouldBe(8);
        accepted.Take(6).ShouldBe(start.GetAllNeighbours());
    }

    [Fact]
    public void Flood_SingleHexMap_ReturnsOnlyTheStartHex()
    {
        var sut = new PathWalker(1, 1);
        var start = new HexCoordinates(1, 1);

        sut.Flood(start, (_, _) => true).ShouldBe([start]);
    }
}
