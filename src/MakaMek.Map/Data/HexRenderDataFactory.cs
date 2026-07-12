using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Map.Services;

namespace Sanet.MakaMek.Map.Data;

/// <summary>
/// Factory for computing <see cref="HexRenderData"/> for individual hexes,
/// centralizing the logic previously inlined in BattleMapView.RenderMap.
/// </summary>
public static class HexRenderDataFactory
{
    /// <summary>
    /// Creates a <see cref="HexRenderData"/> for the hex at <paramref name="coordinates"/> by computing
    /// its edges, water bitmask (when the hex has water terrain), and road/bridge bitmask
    /// (road OR bridge, canonicalized).
    /// </summary>
    /// <param name="map">The battle map containing the hex.</param>
    /// <param name="coordinates">Coordinates of the hex to compute render data for.</param>
    /// <param name="bitmaskService">Service used to compute terrain bitmasks.</param>
    /// <returns>A fully populated <see cref="HexRenderData"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when no hex exists at <paramref name="coordinates"/>.</exception>
    public static HexRenderData Create(
        IBattleMap map,
        HexCoordinates coordinates,
        ITerrainBitmaskService bitmaskService)
    {
        var hex = map.GetHex(coordinates)
            ?? throw new ArgumentException($"No hex found at {coordinates}.", nameof(coordinates));

        var edges = map.GetHexEdges(coordinates);

        CanonicalBitmaskResult? waterBitmask = null;
        if (hex.HasTerrain(MakaMekTerrains.Water))
        {
            waterBitmask = bitmaskService.ComputeCanonicalBitmask(map, coordinates, MakaMekTerrains.Water);
        }

        CanonicalBitmaskResult? roadBitmask = null;
        if (hex.HasTerrain(MakaMekTerrains.Road) || hex.HasTerrain(MakaMekTerrains.Bridge))
        {
            var rawRoad = bitmaskService.ComputeRawBitmask(
                map, coordinates, MakaMekTerrains.Road,
                (current, neighbor) => current.CanRoadConnectTo(neighbor));
            var rawBridge = bitmaskService.ComputeRawBitmask(
                map, coordinates, MakaMekTerrains.Bridge,
                (current, neighbor) => current.CanRoadConnectTo(neighbor));
            roadBitmask = bitmaskService.CanonicalizeRawMask((byte)(rawRoad | rawBridge));
        }

        return new HexRenderData(hex, edges, waterBitmask, roadBitmask);
    }

    /// <summary>
    /// Returns the changed hex and all of its neighbors that are present on the map.
    /// Use this together with <see cref="Create"/> to build the delta set for a selective update.
    /// </summary>
    /// <param name="changedCoord">The coordinate of the hex that changed.</param>
    /// <param name="map">The battle map.</param>
    /// <returns>
    /// An enumerable containing <paramref name="changedCoord"/> (if on the map) followed by
    /// each neighbor coordinate that is also on the map.
    /// </returns>
    public static IEnumerable<HexCoordinates> GetAffectedCoordinates(
        HexCoordinates changedCoord,
        IBattleMap map)
    {
        if (map.IsOnMap(changedCoord))
            yield return changedCoord;

        foreach (var neighbor in changedCoord.GetAllNeighbours())
        {
            if (map.IsOnMap(neighbor))
                yield return neighbor;
        }
    }
}
