using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Services;

/// <summary>
/// Service for computing 6-bit neighbor bitmasks for hex terrain types.
/// Each bit corresponds to a neighbor in HexDirection order
/// (bit 0 = Top, bit 1 = TopRight, … bit 5 = TopLeft).
/// </summary>
public interface ITerrainBitmaskService
{
    /// <summary>
    /// Computes the raw 6-bit bitmask for the neighbors of the hex at
    /// <paramref name="coordinates"/> that contain <paramref name="terrainType"/>.
    /// Bit N is set when the neighbor in direction N has the terrain.
    /// Out-of-bounds neighbors are treated as not having the terrain.
    /// </summary>
    byte ComputeRawBitmask(IBattleMap map, HexCoordinates coordinates, MakaMekTerrains terrainType);

    /// <summary>
    /// Computes the raw bitmask and then canonicalizes it by rotating across all
    /// 6 possible 60° orientations, selecting the lowest numeric value.
    /// </summary>
    CanonicalBitmaskResult ComputeCanonicalBitmask(IBattleMap map, HexCoordinates coordinates, MakaMekTerrains terrainType);
}
