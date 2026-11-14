using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Map.Pathfinding;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Pathfinding;

public class LineOfSightCalculatorTests
{
    [Fact]
    public void Get_ClearPath_ReturnsUnblockedResultWithTargetHex()
    {
        var sut = CreateSut(TraversalTestMap.BuildClearGrid(1, 5));

        var result = sut.Get(new HexCoordinates(1, 1), new HexCoordinates(1, 5), 2, 1, null, null, null, null);

        result.HasLineOfSight.ShouldBeTrue();
        result.AttackerLosLevel.ShouldBe(2);
        result.TargetLosLevel.ShouldBe(1);
        result.TotalInterveningFactor.ShouldBe(0);
        result.HexPath.Select(h => h.Hex.Coordinates).ShouldBe(
        [
            new HexCoordinates(1, 2),
            new HexCoordinates(1, 3),
            new HexCoordinates(1, 4),
            new HexCoordinates(1, 5)
        ]);
    }

    [Fact]
    public void Get_WalksTheSegmentsProducedByLineTo()
    {
        var grid = TraversalTestMap.BuildClearGrid(6, 5);
        var sut = CreateSut(grid);
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(6, 1);

        var result = sut.Get(from, to, 2, 1, null, null, null, null);

        var expectedPath = from.LineTo(to).Select(s => s.MainOption).Skip(1).ToList();
        result.HexPath.Select(h => h.Hex.Coordinates).ShouldBe(expectedPath);
    }

    [Fact]
    public void Get_OffMapCoordinates_ReturnsInvalidCoordinatesBlock()
    {
        var sut = CreateSut(TraversalTestMap.BuildClearGrid(3, 3));
        var from = new HexCoordinates(9, 9);
        var to = new HexCoordinates(1, 1);

        var result = sut.Get(from, to, 0, 0, null, null, null, null);

        result.HasLineOfSight.ShouldBeFalse();
        result.BlockReason.ShouldBe(LineOfSightBlockReason.InvalidCoordinates);
        result.BlockingHexCoordinates.ShouldBe(from);
    }

    [Fact]
    public void Get_MissingInterveningHex_ReturnsInvalidCoordinatesBlock()
    {
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(1, 5);
        var mainLine = from.LineTo(to).Select(s => s.MainOption).ToList();
        var missing = mainLine[2];

        var grid = new HexGrid(1, 5, "test.biome");
        foreach (var coordinates in mainLine.Where(c => c != missing))
        {
            grid.AddHex(TraversalTestMap.CreateHex(coordinates.Q, coordinates.R, 0, new ClearTerrain()));
        }

        var sut = CreateSut(grid);

        var result = sut.Get(from, to, 2, 2, null, null, null, null);

        result.HasLineOfSight.ShouldBeFalse();
        result.BlockReason.ShouldBe(LineOfSightBlockReason.InvalidCoordinates);
        result.BlockingHexCoordinates.ShouldBe(missing);
    }

    [Fact]
    public void Get_AccumulatedTerrainFactor_BlocksWhenThresholdReached()
    {
        var grid = TraversalTestMap.BuildClearGrid(1, 5);
        grid.AddHex(TraversalTestMap.CreateHex(1, 2, 0, new LightWoodsTerrain()));
        grid.AddHex(TraversalTestMap.CreateHex(1, 3, 0, new LightWoodsTerrain()));
        grid.AddHex(TraversalTestMap.CreateHex(1, 4, 0, new LightWoodsTerrain()));
        var sut = CreateSut(grid);

        var result = sut.Get(new HexCoordinates(1, 1), new HexCoordinates(1, 5), 2, 1, null, null, null, null);

        result.HasLineOfSight.ShouldBeFalse();
        result.BlockReason.ShouldBe(LineOfSightBlockReason.InterveningTerrain);
        result.BlockingHexCoordinates.ShouldBe(new HexCoordinates(1, 4));
        result.TotalInterveningFactor.ShouldBe(3);
        result.HexPath.Count.ShouldBe(3);
    }

    [Fact]
    public void Get_HexBaseLevelAboveLosLine_BlocksForElevation()
    {
        var grid = TraversalTestMap.BuildClearGrid(1, 5);
        grid.AddHex(TraversalTestMap.CreateHex(1, 3, 2, new ClearTerrain()));
        var sut = CreateSut(grid);

        var result = sut.Get(new HexCoordinates(1, 1), new HexCoordinates(1, 5), 2, 1, null, null, null, null);

        result.HasLineOfSight.ShouldBeFalse();
        result.BlockReason.ShouldBe(LineOfSightBlockReason.Elevation);
        result.BlockingHexCoordinates.ShouldBe(new HexCoordinates(1, 3));
        result.TotalInterveningFactor.ShouldBe(0);
    }

    [Fact]
    public void Get_SubmergedTarget_BlocksForWaterSubmersion()
    {
        var grid = TraversalTestMap.BuildClearGrid(1, 3);
        grid.AddHex(TraversalTestMap.CreateHex(1, 2, 0, new WaterTerrain(-1)));
        var sut = CreateSut(grid);

        var result = sut.Get(new HexCoordinates(1, 1), new HexCoordinates(1, 2), 1, 0, null, HexSurface.Ground, null, null);

        result.HasLineOfSight.ShouldBeFalse();
        result.BlockReason.ShouldBe(LineOfSightBlockReason.WaterSubmersion);
        result.BlockingHexCoordinates.ShouldBe(new HexCoordinates(1, 2));
    }

    [Fact]
    public void Get_SameHex_ReturnsUnblockedWithSingleTargetHex()
    {
        var sut = CreateSut(TraversalTestMap.BuildClearGrid(3, 3));
        var coordinates = new HexCoordinates(2, 2);

        var result = sut.Get(coordinates, coordinates, 1, 0, null, null, null, null);

        result.HasLineOfSight.ShouldBeTrue();
        result.HexPath.Count.ShouldBe(1);
        result.HexPath[0].Hex.Coordinates.ShouldBe(coordinates);
    }

    [Fact]
    public void Get_DividedLine_ResolvesTheDefenderFavouringOption()
    {
        var grid = TraversalTestMap.BuildClearGrid(10, 10);
        grid.AddHex(TraversalTestMap.CreateHex(3, 3, 0, new HeavyWoodsTerrain()));
        grid.AddHex(TraversalTestMap.CreateHex(4, 2, 0, new HeavyWoodsTerrain()));
        var sut = CreateSut(grid);

        var result = sut.Get(new HexCoordinates(2, 2), new HexCoordinates(6, 2), 2, 0, null, null, null, null);

        result.HasLineOfSight.ShouldBeFalse();
    }

    [Fact]
    public void Get_AirborneBaseLevel_OverridesStandingLevel()
    {
        var sut = CreateSut(TraversalTestMap.BuildGoldenGrid());

        var result = sut.Get(new HexCoordinates(1, 1), new HexCoordinates(6, 5), 1, 1, null, null, 6, null);

        result.HasLineOfSight.ShouldBeTrue();
        result.AttackerLosLevel.ShouldBe(7);
        result.TargetLosLevel.ShouldBe(1);
        result.TotalInterveningFactor.ShouldBe(0);
    }

    [Fact]
    public void Get_WithoutCacheClear_ReusesResolvedPath()
    {
        var grid = TraversalTestMap.BuildClearGrid(10, 10);
        var cache = new LineOfSightCache();
        var sut = new LineOfSightCalculator(grid, cache);
        var from = new HexCoordinates(2, 2);
        var to = new HexCoordinates(6, 2);

        sut.Get(from, to, 2, 0, null, null, null, null).HasLineOfSight.ShouldBeTrue();

        grid.AddHex(TraversalTestMap.CreateHex(3, 3, 0, new HeavyWoodsTerrain()));
        grid.AddHex(TraversalTestMap.CreateHex(4, 2, 0, new HeavyWoodsTerrain()));

        sut.Get(from, to, 2, 0, null, null, null, null).HasLineOfSight.ShouldBeTrue();

        cache.Clear();

        sut.Get(from, to, 2, 0, null, null, null, null).HasLineOfSight.ShouldBeFalse();
    }

    private static LineOfSightCalculator CreateSut(IHexGrid grid)
    {
        return new LineOfSightCalculator(grid, new LineOfSightCache());
    }
}
