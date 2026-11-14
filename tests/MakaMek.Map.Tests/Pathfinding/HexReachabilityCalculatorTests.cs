using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Map.Pathfinding;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Pathfinding;

public class HexReachabilityCalculatorTests
{
    [Fact]
    public void GetReachable_ClearTerrain_ReportsStartAndBudgetCosts()
    {
        var sut = new HexReachabilityCalculator(TraversalTestMap.BuildClearGrid(5, 5), new HexMovementRules());

        var reachable = sut.GetReachable(
            new HexPosition(new HexCoordinates(3, 3), HexDirection.Top),
            maxMovementPoints: 2,
            unitHeight: 1,
            prohibitedHexes: null,
            maxLevelChange: null).ToList();

        reachable.Count.ShouldBe(5);
        reachable.ShouldContain(new HexReachabilityData(new HexCoordinates(3, 3), HexSurface.Ground, 0));
        reachable.Count(r => r.Cost == 1).ShouldBe(1);
        reachable.Count(r => r.Cost == 2).ShouldBe(3);
        reachable.ShouldAllBe(r => r.Cost <= 2);
    }

    [Fact]
    public void GetReachable_ProhibitedHex_IsExcluded()
    {
        var sut = new HexReachabilityCalculator(TraversalTestMap.BuildClearGrid(5, 5), new HexMovementRules());
        var prohibited = new HashSet<HexCoordinates>
        {
            new(2, 1), new(3, 1), new(4, 1), new(1, 2), new(1, 4)
        };

        var reachable = sut.GetReachable(
            new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
            maxMovementPoints: 10,
            unitHeight: 1,
            prohibitedHexes: prohibited,
            maxLevelChange: null).ToList();

        reachable.ShouldNotBeEmpty();
        reachable.ShouldAllBe(r => !prohibited.Contains(r.Coordinates));
    }

    [Fact]
    public void GetReachable_LevelChangeLimit_ExcludesSteepHexes()
    {
        var grid = TraversalTestMap.BuildClearGrid(2, 2);
        grid.AddHex(TraversalTestMap.CreateHex(2, 1, 1, new ClearTerrain()));
        var sut = new HexReachabilityCalculator(grid, new HexMovementRules());

        var reachable = sut.GetReachable(
            new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
            maxMovementPoints: 4,
            unitHeight: 1,
            prohibitedHexes: null,
            maxLevelChange: 0).ToList();

        reachable.ShouldContain(r => r.Coordinates == new HexCoordinates(1, 1));
        reachable.ShouldNotContain(r => r.Coordinates == new HexCoordinates(2, 1));
    }

    [Fact]
    public void GetReachable_UsesInjectedMovementRules()
    {
        var sut = new HexReachabilityCalculator(TraversalTestMap.BuildClearGrid(3, 1), new FixedCostMovementRules(7));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.BottomRight);

        var withBudget = sut.GetReachable(start, 7, 1, null, null).ToList();
        var withoutBudget = sut.GetReachable(start, 6, 1, null, null).ToList();

        withBudget.ShouldContain(new HexReachabilityData(new HexCoordinates(2, 1), HexSurface.Ground, 7));
        withoutBudget.ShouldNotContain(r => r.Coordinates == new HexCoordinates(2, 1));
    }

    [Fact]
    public void GetJumpReachable_FlatMap_ReportsGeometricDistances()
    {
        var sut = new HexReachabilityCalculator(TraversalTestMap.BuildClearGrid(3, 1), new HexMovementRules());

        var reachable = sut.GetJumpReachable(new HexCoordinates(1, 1), 2, null, HexSurface.Ground).ToList();

        reachable.Count.ShouldBe(2);
        reachable.ShouldContain(new HexReachabilityData(new HexCoordinates(2, 1), HexSurface.Ground, 1));
        reachable.ShouldContain(new HexReachabilityData(new HexCoordinates(3, 1), HexSurface.Ground, 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetJumpReachable_NonPositiveMovementPoints_ReturnsEmpty(int movementPoints)
    {
        var sut = new HexReachabilityCalculator(TraversalTestMap.BuildClearGrid(3, 1), new HexMovementRules());

        sut.GetJumpReachable(new HexCoordinates(1, 1), movementPoints, null, HexSurface.Ground).ShouldBeEmpty();
    }

    [Fact]
    public void GetJumpReachable_StartOffMap_ReturnsEmpty()
    {
        var sut = new HexReachabilityCalculator(TraversalTestMap.BuildClearGrid(3, 1), new HexMovementRules());

        sut.GetJumpReachable(new HexCoordinates(9, 9), 3, null, HexSurface.Ground).ShouldBeEmpty();
    }

    [Fact]
    public void GetJumpReachable_HexAboveCeiling_IsNotReachable()
    {
        var grid = TraversalTestMap.BuildClearGrid(3, 1);
        grid.AddHex(TraversalTestMap.CreateHex(2, 1, 2, new ClearTerrain()));
        var sut = new HexReachabilityCalculator(grid, new HexMovementRules());

        sut.GetJumpReachable(new HexCoordinates(1, 1), 1, null, HexSurface.Ground).ShouldBeEmpty();

        var withHigherCeiling = sut.GetJumpReachable(new HexCoordinates(1, 1), 2, null, HexSurface.Ground).ToList();
        withHigherCeiling.ShouldContain(r => r.Coordinates == new HexCoordinates(2, 1));
        withHigherCeiling.ShouldContain(r => r.Coordinates == new HexCoordinates(3, 1));
    }

    [Fact]
    public void GetJumpReachable_ProhibitedHex_OnlyBlocksTheDestination()
    {
        var sut = new HexReachabilityCalculator(TraversalTestMap.BuildClearGrid(3, 1), new HexMovementRules());
        var prohibited = new HashSet<HexCoordinates> { new(2, 1) };

        var reachable = sut.GetJumpReachable(new HexCoordinates(1, 1), 2, prohibited, HexSurface.Ground).ToList();

        reachable.ShouldNotContain(r => r.Coordinates == new HexCoordinates(2, 1));
        reachable.ShouldContain(new HexReachabilityData(new HexCoordinates(3, 1), HexSurface.Ground, 2));
    }

    [Fact]
    public void GetJumpReachable_BridgeDestination_ReportsHighestSurface()
    {
        var grid = TraversalTestMap.BuildClearGrid(2, 1);
        grid.AddHex(TraversalTestMap.CreateHex(2, 1, 0, new ClearTerrain(), new BridgeTerrain(2, 40)));
        var sut = new HexReachabilityCalculator(grid, new HexMovementRules());

        var reachable = sut.GetJumpReachable(new HexCoordinates(1, 1), 2, null, HexSurface.Ground).ToList();

        reachable.ShouldContain(new HexReachabilityData(new HexCoordinates(2, 1), HexSurface.Bridge, 1));
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
