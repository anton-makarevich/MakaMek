using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Pathfinding;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Pathfinding;

/// <summary>
/// Verifies the collaborators the extracted traversal needs are injected into
/// <see cref="BattleMap"/> rather than created inside it.
/// </summary>
public class BattleMapCompositionTests
{
    [Fact]
    public void FindPath_UsesInjectedMovementRules()
    {
        var grid = TraversalTestMap.BuildClearGrid(2, 1);
        var rules = new FixedCostMovementRules(5);
        var sut = CreateMap(grid, rules);
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.BottomRight);
        var target = new HexPosition(new HexCoordinates(2, 1), HexDirection.BottomRight);

        sut.FindPath(start, target, MovementType.Walk, 4, 1).ShouldBeNull();

        var path = sut.FindPath(start, target, MovementType.Walk, 5, 1);
        path.ShouldNotBeNull();
        path.TotalCost.ShouldBe(5);

        // The cached path carries the injected cost, so a budget that only covers the terrain
        // price must not return it. The transition count must not grow, proving the answer came
        // from the cache rather than a fresh search that would also reject the step.
        var callsBeforeCachedLookup = rules.TransitionCalls;
        sut.FindPath(start, target, MovementType.Walk, 1, 1).ShouldBeNull();
        rules.TransitionCalls.ShouldBe(callsBeforeCachedLookup);
    }

    [Fact]
    public void GetReachableHexes_UsesInjectedMovementRules()
    {
        var grid = TraversalTestMap.BuildClearGrid(2, 1);
        var sut = CreateMap(grid, new FixedCostMovementRules(5));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.BottomRight);

        sut.GetReachableHexes(start, 4, 1).ShouldNotContain(r => r.Coordinates == new HexCoordinates(2, 1));
        sut.GetReachableHexes(start, 5, 1).ShouldContain(
            r => r.Coordinates == new HexCoordinates(2, 1) && r.Cost == 5);
    }

    private static BattleMap CreateMap(HexGrid grid, IHexMovementRules rules)
    {
        return new BattleMap(grid, rules);
    }
}
