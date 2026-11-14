using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Map.Pathfinding;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Pathfinding;

public class HexMovementRulesTests
{
    private readonly HexMovementRules _sut = new();

    [Fact]
    public void GetTransitions_ClearFlatHex_ReturnsSingleGroundTransition()
    {
        var from = TraversalTestMap.CreateHex(1, 1, 0, new ClearTerrain());
        var to = TraversalTestMap.CreateHex(2, 1, 0, new ClearTerrain());

        var transitions = _sut.GetTransitions(from, to, HexSurface.Ground, 1, null).ToList();

        transitions.ShouldBe([new HexTransition(HexSurface.Ground, EnterCost: 1, LevelCost: 0)]);
    }

    [Fact]
    public void GetTransitions_EnteringWoods_AddsTerrainMovementCost()
    {
        var from = TraversalTestMap.CreateHex(1, 1, 0, new ClearTerrain());
        var to = TraversalTestMap.CreateHex(2, 1, 0, new HeavyWoodsTerrain());

        var transition = _sut.GetTransitions(from, to, HexSurface.Ground, 1, null).Single();

        transition.EnterCost.ShouldBe(3);
        transition.MovementCost.ShouldBe(3);
    }

    [Fact]
    public void GetTransitions_ClimbingHex_ReportsLevelCost()
    {
        var from = TraversalTestMap.CreateHex(1, 1, 0, new ClearTerrain());
        var to = TraversalTestMap.CreateHex(2, 1, 2, new ClearTerrain());

        var transition = _sut.GetTransitions(from, to, HexSurface.Ground, 1, null).Single();

        transition.LevelCost.ShouldBe(2);
        transition.MovementCost.ShouldBe(3);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(null, true)]
    public void GetTransitions_RespectsMaxLevelChange(int? maxLevelChange, bool expected)
    {
        var from = TraversalTestMap.CreateHex(1, 1, 0, new ClearTerrain());
        var to = TraversalTestMap.CreateHex(2, 1, 2, new ClearTerrain());

        var transitions = _sut.GetTransitions(from, to, HexSurface.Ground, 1, maxLevelChange).ToList();

        transitions.Count.ShouldBe(expected ? 1 : 0);
    }

    [Fact]
    public void GetTransitions_BridgeHexWithShortUnit_OffersGroundOnly()
    {
        var from = TraversalTestMap.CreateHex(1, 1, 0, new ClearTerrain());
        var to = TraversalTestMap.CreateHex(2, 1, 0, new ClearTerrain(), new BridgeTerrain(2, 40));

        var transitions = _sut.GetTransitions(from, to, HexSurface.Ground, 1, null).ToList();

        transitions.ShouldBe([new HexTransition(HexSurface.Ground, EnterCost: 1, LevelCost: 0)]);
    }

    [Fact]
    public void GetTransitions_BridgeHexWithTallUnit_OffersBridgeOnly()
    {
        var from = TraversalTestMap.CreateHex(1, 1, 0, new ClearTerrain());
        var to = TraversalTestMap.CreateHex(2, 1, 0, new ClearTerrain(), new BridgeTerrain(2, 40));

        var transitions = _sut.GetTransitions(from, to, HexSurface.Ground, 3, null).ToList();

        transitions.ShouldBe([new HexTransition(HexSurface.Bridge, EnterCost: 1, LevelCost: 2)]);
    }

    [Fact]
    public void GetTransitions_BridgeHexReachedFromRoad_OffersBothSurfaces()
    {
        var from = TraversalTestMap.CreateHex(1, 1, 0, new RoadTerrain());
        var to = TraversalTestMap.CreateHex(2, 1, 0, new ClearTerrain(), new BridgeTerrain(2, 40));

        var transitions = _sut.GetTransitions(from, to, HexSurface.Ground, 1, null).ToList();

        transitions.Select(t => t.Surface).ShouldBe([HexSurface.Ground, HexSurface.Bridge]);
        transitions.ShouldAllBe(t => t.EnterCost == 1);
    }
}
