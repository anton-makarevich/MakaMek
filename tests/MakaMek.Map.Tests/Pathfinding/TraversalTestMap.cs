using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Tests.Pathfinding;

/// <summary>
/// Golden map shared by the traversal regression tests. It mixes clear ground, two elevations,
/// woods, rough terrain, water and a bridge so every rule the extracted algorithms depend on is
/// exercised.
/// </summary>
internal static class TraversalTestMap
{
    public static BattleMap BuildGoldenMap()
    {
        var map = BuildFlatMap(6, 5);

        SetLevel(map, 2, 2, 1);
        SetLevel(map, 3, 2, 2);
        SetLevel(map, 3, 3, 1);

        AddTerrain(map, 2, 3, new HeavyWoodsTerrain());
        AddTerrain(map, 4, 2, new LightWoodsTerrain());
        AddTerrain(map, 4, 4, new RoughTerrain());
        AddTerrain(map, 5, 3, new WaterTerrain(-1));
        AddTerrain(map, 5, 4, new WaterTerrain(-1));
        AddTerrain(map, 5, 4, new BridgeTerrain(2, 40));

        return map;
    }

    /// <summary>
    /// The same terrain layout as <see cref="BuildGoldenMap"/>, exposed as the narrow grid the
    /// extracted collaborators consume.
    /// </summary>
    public static HexGrid BuildGoldenGrid()
    {
        var grid = new HexGrid(6, 5, "test.biome");
        for (var q = 1; q <= 6; q++)
        {
            for (var r = 1; r <= 5; r++)
            {
                grid.AddHex(new Hex(new HexCoordinates(q, r)).WithTerrain(new ClearTerrain()));
            }
        }

        SetLevel(grid, 2, 2, 1);
        SetLevel(grid, 3, 2, 2);
        SetLevel(grid, 3, 3, 1);

        AddTerrain(grid, 2, 3, new HeavyWoodsTerrain());
        AddTerrain(grid, 4, 2, new LightWoodsTerrain());
        AddTerrain(grid, 4, 4, new RoughTerrain());
        AddTerrain(grid, 5, 3, new WaterTerrain(-1));
        AddTerrain(grid, 5, 4, new WaterTerrain(-1));
        AddTerrain(grid, 5, 4, new BridgeTerrain(2, 40));

        return grid;
    }

    public static BattleMap BuildFlatMap(int width, int height)
    {
        var map = new BattleMap(width, height);
        for (var q = 1; q <= width; q++)
        {
            for (var r = 1; r <= height; r++)
            {
                map.AddHex(new Hex(new HexCoordinates(q, r)).WithTerrain(new ClearTerrain()));
            }
        }

        return map;
    }

    public static HexGrid BuildClearGrid(int width, int height)
    {
        var grid = new HexGrid(width, height, "test.biome");
        for (var q = 1; q <= width; q++)
        {
            for (var r = 1; r <= height; r++)
            {
                grid.AddHex(new Hex(new HexCoordinates(q, r)).WithTerrain(new ClearTerrain()));
            }
        }

        return grid;
    }

    public static Hex CreateHex(int q, int r, int level = 0, params Terrain[] terrains)
    {
        var hex = new Hex(new HexCoordinates(q, r), level);
        foreach (var terrain in terrains)
        {
            hex.AddTerrain(terrain);
        }

        return hex;
    }

    private static void SetLevel(BattleMap map, int q, int r, int level)
    {
        map.GetHex(new HexCoordinates(q, r))!.Level = level;
    }

    private static void AddTerrain(BattleMap map, int q, int r, Terrain terrain)
    {
        map.GetHex(new HexCoordinates(q, r))!.AddTerrain(terrain);
    }

    private static void SetLevel(HexGrid grid, int q, int r, int level)
    {
        grid.GetHex(new HexCoordinates(q, r))!.Level = level;
    }

    private static void AddTerrain(HexGrid grid, int q, int r, Terrain terrain)
    {
        grid.GetHex(new HexCoordinates(q, r))!.AddTerrain(terrain);
    }
}
