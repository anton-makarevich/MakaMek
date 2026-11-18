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
        ApplyGoldenLayout(map.GetHex);
        return map;
    }

    /// <summary>
    /// The same terrain layout as <see cref="BuildGoldenMap"/>, exposed as the narrow grid the
    /// extracted collaborators consume.
    /// </summary>
    public static HexGrid BuildGoldenGrid()
    {
        var grid = BuildClearGrid(6, 5);
        ApplyGoldenLayout(grid.GetHex);
        return grid;
    }

    /// <summary>
    /// Single source of truth for the golden map's levels and terrain. Each builder creates fresh
    /// hexes and stamps its own biome before this layout is applied.
    /// </summary>
    private static void ApplyGoldenLayout(Func<HexCoordinates, Hex?> getHex)
    {
        SetLevel(getHex, 2, 2, 1);
        SetLevel(getHex, 3, 2, 2);
        SetLevel(getHex, 3, 3, 1);

        AddTerrain(getHex, 2, 3, new HeavyWoodsTerrain());
        AddTerrain(getHex, 4, 2, new LightWoodsTerrain());
        AddTerrain(getHex, 4, 4, new RoughTerrain());
        AddTerrain(getHex, 5, 3, new WaterTerrain(-1));
        AddTerrain(getHex, 5, 4, new WaterTerrain(-1));
        AddTerrain(getHex, 5, 4, new BridgeTerrain(2, 40));
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

    private static void SetLevel(Func<HexCoordinates, Hex?> getHex, int q, int r, int level)
    {
        getHex(new HexCoordinates(q, r))!.Level = level;
    }

    private static void AddTerrain(Func<HexCoordinates, Hex?> getHex, int q, int r, Terrain terrain)
    {
        getHex(new HexCoordinates(q, r))!.AddTerrain(terrain);
    }
}
