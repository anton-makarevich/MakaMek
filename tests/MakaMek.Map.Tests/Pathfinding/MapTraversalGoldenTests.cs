using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Pathfinding;

/// <summary>
/// Golden-map regression coverage for the public <see cref="BattleMap"/> API. These expectations
/// were recorded before the pathfinder, reachability and LOS extractions and must not change:
/// identical movement costs, reachability, jump reach and LOS results.
/// </summary>
public class MapTraversalGoldenTests
{
    [Fact]
    public void GetReachableHexes_ReturnsTheGoldenCosts()
    {
        var sut = TraversalTestMap.BuildGoldenMap();

        var reachable = sut.GetReachableHexes(
            new HexPosition(new HexCoordinates(1, 1), HexDirection.Top), 6, 2);

        Normalise(reachable).ShouldBe(
        [
            (1, 1, HexSurface.Ground, 0),
            (1, 2, HexSurface.Ground, 4),
            (1, 3, HexSurface.Ground, 5),
            (1, 4, HexSurface.Ground, 6),
            (2, 1, HexSurface.Ground, 3),
            (2, 2, HexSurface.Ground, 6),
            (3, 1, HexSurface.Ground, 5),
            (3, 2, HexSurface.Ground, 6)
        ]);
    }

    [Fact]
    public void GetReachableHexes_WithProhibitedHexes_ReturnsTheGoldenCosts()
    {
        var sut = TraversalTestMap.BuildGoldenMap();
        var prohibited = new HashSet<HexCoordinates> { new(3, 2), new(2, 1) };

        var reachable = sut.GetReachableHexes(
            new HexPosition(new HexCoordinates(1, 1), HexDirection.Top), 6, 2, prohibited);

        Normalise(reachable).ShouldBe(
        [
            (1, 1, HexSurface.Ground, 0),
            (1, 2, HexSurface.Ground, 4),
            (1, 3, HexSurface.Ground, 5),
            (1, 4, HexSurface.Ground, 6)
        ]);
    }

    [Fact]
    public void GetReachableHexes_WithMaxLevelChange_ReturnsTheGoldenCosts()
    {
        var sut = TraversalTestMap.BuildGoldenMap();

        var reachable = sut.GetReachableHexes(
            new HexPosition(new HexCoordinates(1, 1), HexDirection.Top), 8, 2, null, 1);

        Normalise(reachable).ShouldBe(
        [
            (1, 1, HexSurface.Ground, 0),
            (1, 2, HexSurface.Ground, 4),
            (1, 3, HexSurface.Ground, 5),
            (1, 4, HexSurface.Ground, 6),
            (1, 5, HexSurface.Ground, 7),
            (2, 1, HexSurface.Ground, 3),
            (2, 2, HexSurface.Ground, 6),
            (2, 4, HexSurface.Ground, 8),
            (3, 1, HexSurface.Ground, 5),
            (3, 3, HexSurface.Ground, 8),
            (4, 1, HexSurface.Ground, 7),
            (5, 2, HexSurface.Ground, 8)
        ]);
    }

    [Fact]
    public void FindPath_Shortest_ReturnsTheGoldenPath()
    {
        var sut = TraversalTestMap.BuildGoldenMap();

        var path = sut.FindPath(
            new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
            new HexPosition(new HexCoordinates(6, 5), HexDirection.Top),
            MovementType.Walk, 20, 2);

        path.ShouldNotBeNull();
        path.TotalCost.ShouldBe(16);
        path.HexesTraveled.ShouldBe(8);
        path.Segments.Select(s => (s.To.Coordinates, s.To.Facing)).ShouldBe(
        [
            (new HexCoordinates(1, 1), HexDirection.TopRight),
            (new HexCoordinates(1, 1), HexDirection.BottomRight),
            (new HexCoordinates(2, 1), HexDirection.BottomRight),
            (new HexCoordinates(2, 1), HexDirection.TopRight),
            (new HexCoordinates(3, 1), HexDirection.TopRight),
            (new HexCoordinates(3, 1), HexDirection.BottomRight),
            (new HexCoordinates(4, 1), HexDirection.BottomRight),
            (new HexCoordinates(5, 2), HexDirection.BottomRight),
            (new HexCoordinates(6, 2), HexDirection.BottomRight),
            (new HexCoordinates(6, 2), HexDirection.Bottom),
            (new HexCoordinates(6, 3), HexDirection.Bottom),
            (new HexCoordinates(6, 4), HexDirection.Bottom),
            (new HexCoordinates(6, 5), HexDirection.Bottom),
            (new HexCoordinates(6, 5), HexDirection.BottomLeft),
            (new HexCoordinates(6, 5), HexDirection.TopLeft),
            (new HexCoordinates(6, 5), HexDirection.Top)
        ]);
    }

    [Fact]
    public void FindPath_FromBridge_ReturnsTheGoldenSegmentCosts()
    {
        var sut = TraversalTestMap.BuildGoldenMap();

        var path = sut.FindPath(
            new HexPosition(new HexCoordinates(5, 4), HexDirection.Bottom, HexSurface.Bridge),
            new HexPosition(new HexCoordinates(1, 2), HexDirection.Top),
            MovementType.Walk, 20, 2);

        path.ShouldNotBeNull();
        path.TotalCost.ShouldBe(11);
        path.HexesTraveled.ShouldBe(4);
        path.Segments.Select(s => s.Cost).ShouldBe([1, 1, 3, 2, 1, 2, 1]);
        path.Segments.Select(s => s.ElevationChange).ShouldBe([0, 0, -2, 1, 0, -1, 0]);
        path.Segments.Select(s => (s.To.Coordinates, s.To.Surface)).ShouldBe(
        [
            (new HexCoordinates(5, 4), HexSurface.Bridge),
            (new HexCoordinates(5, 4), HexSurface.Bridge),
            (new HexCoordinates(4, 3), HexSurface.Ground),
            (new HexCoordinates(3, 3), HexSurface.Ground),
            (new HexCoordinates(2, 2), HexSurface.Ground),
            (new HexCoordinates(1, 2), HexSurface.Ground),
            (new HexCoordinates(1, 2), HexSurface.Ground)
        ]);
    }

    [Fact]
    public void FindPath_LongestWithInsufficientBudget_ReturnsNull()
    {
        var sut = TraversalTestMap.BuildGoldenMap();

        var path = sut.FindPath(
            new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
            new HexPosition(new HexCoordinates(6, 5), HexDirection.Top),
            MovementType.Walk, 12, 2, null, PathFindingMode.Longest);

        path.ShouldBeNull();
    }

    [Fact]
    public void GetJumpReachableHexes_ReturnsTheGoldenCosts()
    {
        var sut = TraversalTestMap.BuildGoldenMap();

        var reachable = sut.GetJumpReachableHexes(new HexCoordinates(2, 2), 4);

        Normalise(reachable).ShouldBe(
        [
            (1, 1, HexSurface.Ground, 2),
            (1, 2, HexSurface.Ground, 1),
            (1, 3, HexSurface.Ground, 1),
            (1, 4, HexSurface.Ground, 2),
            (1, 5, HexSurface.Ground, 3),
            (2, 1, HexSurface.Ground, 1),
            (2, 3, HexSurface.Ground, 1),
            (2, 4, HexSurface.Ground, 2),
            (2, 5, HexSurface.Ground, 3),
            (3, 1, HexSurface.Ground, 2),
            (3, 2, HexSurface.Ground, 1),
            (3, 3, HexSurface.Ground, 1),
            (3, 4, HexSurface.Ground, 2),
            (3, 5, HexSurface.Ground, 3),
            (4, 1, HexSurface.Ground, 2),
            (4, 2, HexSurface.Ground, 2),
            (4, 3, HexSurface.Ground, 2),
            (4, 4, HexSurface.Ground, 3),
            (4, 5, HexSurface.Ground, 4),
            (5, 1, HexSurface.Ground, 3),
            (5, 2, HexSurface.Ground, 3),
            (5, 3, HexSurface.Ground, 3),
            (5, 4, HexSurface.Bridge, 3),
            (5, 5, HexSurface.Ground, 4),
            (6, 1, HexSurface.Ground, 4),
            (6, 2, HexSurface.Ground, 4),
            (6, 3, HexSurface.Ground, 4),
            (6, 4, HexSurface.Ground, 4)
        ]);
    }

    [Fact]
    public void GetLineOfSight_BlockedByHill_ReturnsTheGoldenPath()
    {
        var sut = TraversalTestMap.BuildGoldenMap();

        var result = sut.GetLineOfSight(
            new HexCoordinates(1, 1), new HexCoordinates(6, 5), 2, 1);

        result.HasLineOfSight.ShouldBeFalse();
        result.BlockReason.ShouldBe(LineOfSightBlockReason.Elevation);
        result.BlockingHexCoordinates.ShouldBe(new HexCoordinates(3, 2));
        result.AttackerLosLevel.ShouldBe(2);
        result.TargetLosLevel.ShouldBe(1);
        result.TotalInterveningFactor.ShouldBe(0);
        result.HexPath.Select(h => h.Hex.Coordinates).ShouldBe(
        [
            new HexCoordinates(2, 1),
            new HexCoordinates(2, 2),
            new HexCoordinates(3, 2)
        ]);
    }

    [Fact]
    public void GetLineOfSight_BridgeEndpoint_ReturnsTheGoldenInterveningFactor()
    {
        var sut = TraversalTestMap.BuildGoldenMap();

        var result = sut.GetLineOfSight(
            new HexCoordinates(5, 4), new HexCoordinates(1, 4), 0, 0,
            HexSurface.Bridge, HexSurface.Ground);

        result.HasLineOfSight.ShouldBeTrue();
        result.AttackerLosLevel.ShouldBe(2);
        result.TargetLosLevel.ShouldBe(0);
        result.TotalInterveningFactor.ShouldBe(2);
        result.HexPath.Select(h => (h.Hex.Coordinates, h.InterveningFactor)).ShouldBe(
        [
            (new HexCoordinates(4, 3), 0),
            (new HexCoordinates(3, 4), 0),
            (new HexCoordinates(2, 3), 2),
            (new HexCoordinates(1, 4), 0)
        ]);
    }

    private static (int Q, int R, HexSurface Surface, int Cost)[] Normalise(
        IEnumerable<HexReachabilityData> data)
    {
        return data
            .OrderBy(entry => entry.Coordinates.Q)
            .ThenBy(entry => entry.Coordinates.R)
            .ThenBy(entry => (int)entry.Surface)
            .Select(entry => (entry.Coordinates.Q, entry.Coordinates.R, entry.Surface, entry.Cost))
            .ToArray();
    }
}
