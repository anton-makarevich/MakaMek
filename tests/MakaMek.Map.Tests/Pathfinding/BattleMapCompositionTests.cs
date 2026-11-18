using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
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
        var sut = CreateMap(grid, new FixedCostMovementRules(5));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.BottomRight);
        var target = new HexPosition(new HexCoordinates(2, 1), HexDirection.BottomRight);

        sut.FindPath(start, target, MovementType.Walk, 4, 1).ShouldBeNull();

        var path = sut.FindPath(start, target, MovementType.Walk, 5, 1);
        path.ShouldNotBeNull();
        path.TotalCost.ShouldBe(5);

        // The cached path carries the injected cost, so a budget that only covers the terrain
        // price must not return it.
        sut.FindPath(start, target, MovementType.Walk, 1, 1).ShouldBeNull();
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
                yield return new HexTransition(
                    surface,
                    [new HexEnterMovementCost { Value = cost }],
                    0);
            }
        }
    }
}
