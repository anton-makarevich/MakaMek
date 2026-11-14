using Sanet.MakaMek.Assets.Models.Terrains;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Translates terrain-level requests (base terrain, overlay type, hex edge, bitmask texture)
/// into the package asset coordinates the cache looks images up by.
/// </summary>
public interface ITerrainImageResolver
{
    /// <summary>
    /// Resolves the base terrain image of a biome.
    /// </summary>
    /// <param name="variant">Optional variant number (deterministically selected if not specified)</param>
    TerrainImageRequest ResolveBase(int? variant = null);

    /// <summary>
    /// Resolves the overlay image of a terrain type (e.g. "lightwoods", "rough").
    /// </summary>
    /// <param name="terrainType">Terrain type name</param>
    /// <param name="variant">Optional variant number (deterministically selected if not specified)</param>
    TerrainImageRequest ResolveOverlay(string terrainType, int? variant = null);

    /// <summary>
    /// Resolves a hex edge image request. Returns <c>null</c> for asset types that are not hex edges.
    /// The hex coordinates seed the deterministic variant selection so a hex always renders the same edge.
    /// </summary>
    /// <param name="direction">Hex edge direction (0-5)</param>
    /// <param name="edgeType">Edge type (top or bottom)</param>
    /// <param name="coordinates">Hex coordinates used for deterministic variant selection</param>
    TerrainImageRequest? ResolveEdge(HexDirection direction, TerrainAssetType edgeType, HexCoordinates coordinates);

    /// <summary>
    /// Resolves the water bitmask texture of a canonical bitmask.
    /// </summary>
    /// <param name="canonicalBitmask">The canonical bitmask result containing the mask and rotation</param>
    TerrainImageRequest ResolveWaterTexture(CanonicalBitmaskResult canonicalBitmask);

    /// <summary>
    /// Resolves the road/bridge bitmask texture of a canonical bitmask.
    /// </summary>
    /// <param name="canonicalBitmask">The canonical bitmask result containing the mask and rotation</param>
    TerrainImageRequest ResolveRoadTexture(CanonicalBitmaskResult canonicalBitmask);
}
