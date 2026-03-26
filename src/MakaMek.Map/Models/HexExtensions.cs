using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Core.Utils;

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
}
