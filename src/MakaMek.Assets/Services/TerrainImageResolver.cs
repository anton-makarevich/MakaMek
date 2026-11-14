using Sanet.MakaMek.Assets.Models.Terrains;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Maps terrain-level requests to the package asset coordinates used by the terrain cache.
/// The mapping is pure and stateless, so a single instance is safe to share.
/// </summary>
public sealed class TerrainImageResolver : ITerrainImageResolver
{
    private const string BaseAssetName = "base";
    private const int BitmaskNameLength = 6;
    private const int EdgeVariantSeedRowFactor = 31;

    /// <inheritdoc />
    public TerrainImageRequest ResolveBase(int? variant = null) =>
        new(TerrainAssetType.Base, BaseAssetName, Variant: variant);

    /// <inheritdoc />
    public TerrainImageRequest ResolveOverlay(string terrainType, int? variant = null) =>
        new(TerrainAssetType.Overlay, terrainType, Variant: variant);

    /// <inheritdoc />
    public TerrainImageRequest? ResolveEdge(
        HexDirection direction,
        TerrainAssetType edgeType,
        HexCoordinates coordinates)
    {
        if (edgeType is not (TerrainAssetType.EdgeTop or TerrainAssetType.EdgeBottom))
            return null;

        var directionName = ((int)direction).ToString();

        // Hex coordinates provide a stable, per-hex seed for variant selection.
        return new TerrainImageRequest(
            edgeType,
            directionName,
            coordinates.Q + coordinates.R * EdgeVariantSeedRowFactor);
    }

    /// <inheritdoc />
    public TerrainImageRequest ResolveWaterTexture(CanonicalBitmaskResult canonicalBitmask) =>
        new(TerrainAssetType.Water, ToBitmaskName(canonicalBitmask));

    /// <inheritdoc />
    public TerrainImageRequest ResolveRoadTexture(CanonicalBitmaskResult canonicalBitmask) =>
        new(TerrainAssetType.Road, ToBitmaskName(canonicalBitmask));

    /// <summary>
    /// Converts a canonical mask to the 6-digit binary file name used inside packages
    /// (e.g. mask 1 → "000001").
    /// </summary>
    private static string ToBitmaskName(CanonicalBitmaskResult canonicalBitmask) =>
        Convert.ToString(canonicalBitmask.CanonicalMask, 2).PadLeft(BitmaskNameLength, '0');
}
