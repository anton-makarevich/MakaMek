using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Services;

// Note: BattleMap.GetHexEdges() and HexagonGeometry.GetCorners() are candidates for
// future refactoring to use this same bitmask infrastructure
/// <inheritdoc />
public class TerrainBitmaskService : ITerrainBitmaskService
{
    /// <inheritdoc />
    public byte ComputeRawBitmask(IBattleMap map, HexCoordinates coordinates, MakaMekTerrains terrainType, Func<Hex, Hex, bool>? neighborFilter = null)
    {
        byte mask = 0;
        var directions = HexDirectionExtensions.AllDirections;
        var currentHex = map.GetHex(coordinates);

        for (var i = 0; i < directions.Length; i++)
        {
            var neighborCoords = coordinates.GetNeighbour(directions[i]);
            var neighborHex = map.GetHex(neighborCoords);
            if (neighborHex != null && neighborHex.HasTerrain(terrainType)
                                     && (neighborFilter == null || (currentHex != null && neighborFilter(currentHex, neighborHex))))
            {
                mask |= (byte)(1 << i);
            }
        }

        return mask;
    }

    /// <inheritdoc />
    public byte ComputeBoundaryMask(HexCoordinates coordinates, IReadOnlySet<HexCoordinates> coordinatesSet)
    {
        return ComputeBoundaryMask(coordinates, coordinatesSet.Contains);
    }

    /// <inheritdoc />
    public byte ComputeBoundaryMask(HexCoordinates coordinates, Func<HexCoordinates, bool> containsCoordinate)
    {
        byte mask = 0;
        var directions = HexDirectionExtensions.AllDirections;

        for (var i = 0; i < directions.Length; i++)
        {
            var neighborCoords = coordinates.GetNeighbour(directions[i]);
            if (!containsCoordinate(neighborCoords))
            {
                mask |= (byte)(1 << i);
            }
        }

        return mask;
    }

    /// <inheritdoc />
    public CanonicalBitmaskResult ComputeCanonicalBitmask(IBattleMap map, HexCoordinates coordinates, MakaMekTerrains terrainType, Func<Hex, Hex, bool>? neighborFilter = null)
    {
        var raw = ComputeRawBitmask(map, coordinates, terrainType, neighborFilter);
        return Canonicalize(raw);
    }

    /// <inheritdoc />
    public CanonicalBitmaskResult CanonicalizeRawMask(byte rawMask)
    {
        return Canonicalize(rawMask);
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

    /// <inheritdoc />
    public HexRenderData CreateHexRenderData(IBattleMap map, HexCoordinates coordinates)
    {
        var hex = map.GetHex(coordinates)
            ?? throw new ArgumentException($"No hex found at {coordinates}.", nameof(coordinates));

        var edges = map.GetHexEdges(coordinates);

        CanonicalBitmaskResult? waterBitmask = null;
        if (hex.HasTerrain(MakaMekTerrains.Water))
        {
            waterBitmask = ComputeCanonicalBitmask(map, coordinates, MakaMekTerrains.Water);
        }

        CanonicalBitmaskResult? roadBitmask = null;
        if (!hex.HasTerrain(MakaMekTerrains.Road) && !hex.HasTerrain(MakaMekTerrains.Bridge))
            return new HexRenderData(hex, edges, waterBitmask, roadBitmask);
        var rawRoad = ComputeRawBitmask(map, coordinates, MakaMekTerrains.Road,
            (current, neighbor) => current.CanRoadConnectTo(neighbor));
        var rawBridge = ComputeRawBitmask(map, coordinates, MakaMekTerrains.Bridge,
            (current, neighbor) => current.CanRoadConnectTo(neighbor));
        roadBitmask = CanonicalizeRawMask((byte)(rawRoad | rawBridge));

        return new HexRenderData(hex, edges, waterBitmask, roadBitmask);
    }
}
