using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Pathfinding;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Pathfinding;

public class HexPathfinderTests
{
    [Fact]
    public void Find_FlatMap_ReturnsShortestPathWithTurningSegments()
    {
        var sut = new HexPathfinder(TraversalTestMap.BuildClearGrid(3, 1), new HexMovementRules());
        var request = Request(
            new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
            new HexPosition(new HexCoordinates(3, 1), HexDirection.Bottom));

        var path = sut.Find(request, new MovementPathCache());

        path.ShouldNotBeNull();
        path.TotalCost.ShouldBe(7);
        path.Segments.Select(s => (s.To.Coordinates, s.To.Facing)).ShouldBe(
        [
            (new HexCoordinates(1, 1), HexDirection.TopRight),
            (new HexCoordinates(1, 1), HexDirection.BottomRight),
            (new HexCoordinates(2, 1), HexDirection.BottomRight),
            (new HexCoordinates(2, 1), HexDirection.TopRight),
            (new HexCoordinates(3, 1), HexDirection.TopRight),
            (new HexCoordinates(3, 1), HexDirection.BottomRight),
            (new HexCoordinates(3, 1), HexDirection.Bottom)
        ]);
    }

    [Fact]
    public void Find_ProhibitedHex_ReturnsNullWhenNoRouteExists()
    {
        var sut = new HexPathfinder(TraversalTestMap.BuildClearGrid(3, 1), new HexMovementRules());
        var request = Request(
            new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
            new HexPosition(new HexCoordinates(3, 1), HexDirection.Top),
            prohibited: new HashSet<HexCoordinates> { new(2, 1) });

        sut.Find(request, new MovementPathCache()).ShouldBeNull();
    }

    [Fact]
    public void Find_InsufficientBudget_ReturnsNull()
    {
        var sut = new HexPathfinder(TraversalTestMap.BuildClearGrid(3, 1), new HexMovementRules());
        var request = Request(
            new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
            new HexPosition(new HexCoordinates(3, 1), HexDirection.Top),
            maxMovementPoints: 2);

        sut.Find(request, new MovementPathCache()).ShouldBeNull();
    }

    [Fact]
    public void Find_SameHex_ReturnsTurningSegments()
    {
        var sut = new HexPathfinder(TraversalTestMap.BuildClearGrid(2, 2), new HexMovementRules());
        var request = Request(
            new HexPosition(new HexCoordinates(2, 2), HexDirection.Top),
            new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom));

        var path = sut.Find(request, new MovementPathCache());

        path.ShouldNotBeNull();
        path.TotalCost.ShouldBe(3);
        path.Segments.Count.ShouldBe(3);
    }

    [Fact]
    public void Find_SameHexOnDifferentSurfaces_ReturnsNull()
    {
        var sut = new HexPathfinder(TraversalTestMap.BuildClearGrid(2, 2), new HexMovementRules());
        var request = Request(
            new HexPosition(new HexCoordinates(2, 2), HexDirection.Top),
            new HexPosition(new HexCoordinates(2, 2), HexDirection.Top, HexSurface.Bridge));

        sut.Find(request, new MovementPathCache()).ShouldBeNull();
    }

    [Fact]
    public void Find_LongestMode_PrefersTheLoopWithMoreHexes()
    {
        var grid = TraversalTestMap.BuildClearGrid(2, 2);
        var sut = new HexPathfinder(grid, new HexMovementRules());
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom);

        var shortest = sut.Find(Request(start, target, maxMovementPoints: 12), new MovementPathCache());
        var longest = sut.Find(
            Request(start, target, maxMovementPoints: 12, mode: PathFindingMode.Longest),
            new MovementPathCache());

        shortest.ShouldNotBeNull();
        longest.ShouldNotBeNull();
        shortest.HexesTraveled.ShouldBe(2);
        longest.HexesTraveled.ShouldBe(3);
    }

    [Fact]
    public void Find_UsesInjectedMovementRules()
    {
        var grid = TraversalTestMap.BuildClearGrid(2, 1);
        var sut = new HexPathfinder(grid, new FixedCostMovementRules(4));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.BottomRight);
        var target = new HexPosition(new HexCoordinates(2, 1), HexDirection.BottomRight);

        sut.Find(Request(start, target, maxMovementPoints: 3), new MovementPathCache()).ShouldBeNull();

        sut.Find(Request(start, target, maxMovementPoints: 4), new MovementPathCache()).ShouldNotBeNull();
    }

    [Fact]
    public void Find_CachesShortestPath()
    {
        var sut = new HexPathfinder(TraversalTestMap.BuildClearGrid(5, 5), new HexMovementRules());
        var cache = new MovementPathCache();
        var request = Request(
            new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
            new HexPosition(new HexCoordinates(3, 3), HexDirection.Bottom),
            maxMovementPoints: 10);

        var first = sut.Find(request, cache);
        var second = sut.Find(request, cache);

        second.ShouldBeSameAs(first);
    }

    [Fact]
    public void Find_WithProhibitedHexes_DoesNotUseCache()
    {
        var sut = new HexPathfinder(TraversalTestMap.BuildClearGrid(5, 5), new HexMovementRules());
        var cache = new MovementPathCache();
        var request = Request(
            new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
            new HexPosition(new HexCoordinates(3, 3), HexDirection.Bottom),
            maxMovementPoints: 10,
            prohibited: new HashSet<HexCoordinates> { new(5, 5) });

        var first = sut.Find(request, cache);
        var second = sut.Find(request, cache);

        first.ShouldNotBeNull();
        second.ShouldNotBeNull();
        second.ShouldNotBeSameAs(first);
    }

    [Fact]
    public void Find_ZeroBudget_ReturnsNullForNonZeroCostPath()
    {
        var sut = new HexPathfinder(TraversalTestMap.BuildClearGrid(2, 1), new HexMovementRules());
        var request = Request(
            new HexPosition(new HexCoordinates(1, 1), HexDirection.BottomRight),
            new HexPosition(new HexCoordinates(2, 1), HexDirection.BottomRight),
            maxMovementPoints: 0);

        sut.Find(request, new MovementPathCache()).ShouldBeNull();
    }

    private static PathRequest Request(
        HexPosition start,
        HexPosition target,
        int maxMovementPoints = 20,
        int unitHeight = 1,
        IReadOnlySet<HexCoordinates>? prohibited = null,
        PathFindingMode mode = PathFindingMode.Shortest,
        int? maxLevelChange = null)
    {
        return new PathRequest(
            mode,
            start,
            target,
            MovementType.Walk,
            maxMovementPoints,
            unitHeight,
            prohibited,
            maxLevelChange);
    }

    private sealed class FixedCostMovementRules(int cost) : IHexMovementRules
    {
        public IEnumerable<HexTransition> GetTransitions(
            Hex fromHex,
            Hex toHex,
            HexSurface fromSurface,
            int unitHeight,
            int? maxLevelChange)
        {
            foreach (var surface in toHex.GetHexSurfaces())
            {
                yield return new HexTransition(surface, cost, 0);
            }
        }
    }
}
