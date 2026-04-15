using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Services;

// Note: BattleMap.GetHexEdges() and HexControl.UpdateEdgeLayers() are candidates for
// future refactoring to use this same bitmask infrastructure
/// <inheritdoc />
public class TerrainBitmaskService : ITerrainBitmaskService
{
    /// <inheritdoc />
    public byte ComputeRawBitmask(IBattleMap map, HexCoordinates coordinates, MakaMekTerrains terrainType)
    {
        byte mask = 0;
        var directions = HexDirectionExtensions.AllDirections;

        for (var i = 0; i < directions.Length; i++)
        {
            var neighborCoords = coordinates.GetNeighbour(directions[i]);
            var neighborHex = map.GetHex(neighborCoords);
            if (neighborHex != null && neighborHex.HasTerrain(terrainType))
            {
                mask |= (byte)(1 << i);
            }
        }

        return mask;
    }

    /// <inheritdoc />
    public CanonicalBitmaskResult ComputeCanonicalBitmask(IBattleMap map, HexCoordinates coordinates, MakaMekTerrains terrainType)
    {
        var raw = ComputeRawBitmask(map, coordinates, terrainType);
        return Canonicalize(raw);
    }

    /// <summary>
    /// Rotates a 6-bit bitmask by <paramref name="steps"/> positions clockwise.
    /// Each step shifts each bit from direction N to direction (N+1) mod 6.
    /// </summary>
    private static byte RotateMask(byte mask, int steps)
    {
        steps = (steps % 6 + 6) % 6;
        // Rotate within the lower 6 bits
        return (byte)(((mask << steps) | (mask >> (6 - steps))) & 0x3F);
    }

    /// <summary>
    /// Returns the canonical (lowest-value) rotation of the 6-bit bitmask,
    /// along with the number of clockwise rotation steps required.
    /// </summary>
    private static CanonicalBitmaskResult Canonicalize(byte mask)
    {
        var canonical = mask;
        var rotationSteps = 0;

        for (var step = 1; step < 6; step++)
        {
            var rotated = RotateMask(mask, step);
            if (rotated >= canonical) continue;
            canonical = rotated;
            rotationSteps = step;
        }

        return new CanonicalBitmaskResult(canonical, rotationSteps);
    }
}
