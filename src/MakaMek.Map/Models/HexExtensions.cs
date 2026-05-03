using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Models;

public static class HexExtensions
{
    public static Hex WithTerrain(this Hex hex, Terrain terrain)
    {
        hex.AddTerrain(terrain);
        return hex;
    }

    public static Hex WithLevel(this Hex hex, int level)
    {
        hex.Level = level;
        return hex;
    }

    /// <summary>
    /// Gets the water depth of the hex as a positive integer.
    /// Returns null if the hex has no water terrain.
    /// Returns 0 for shallow water (Height 0), 1 for depth -1, 2 for depth -2, etc.
    /// </summary>
    public static int? GetWaterDepth(this Hex hex)
    {
        if (!hex.HasTerrain(MakaMekTerrains.Water))
            return null;
        var waterTerrain = hex.GetTerrain(MakaMekTerrains.Water);
        return waterTerrain is null ? 0 : -1 * waterTerrain.Height;
    }
}
