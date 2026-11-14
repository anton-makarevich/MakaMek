using Sanet.MakaMek.Assets.Models.Terrains;

namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Coordinates of a single terrain image inside an MMTX package.
/// </summary>
/// <param name="AssetType">Kind of terrain asset to look up</param>
/// <param name="AssetName">Asset name as stored in the package (terrain type, edge direction or bitmask)</param>
/// <param name="SelectionSeed">Seed for deterministic variant selection when no variant is requested</param>
/// <param name="Variant">Explicitly requested variant, or <c>null</c> to select one deterministically</param>
public readonly record struct TerrainImageRequest(
    TerrainAssetType AssetType,
    string AssetName,
    int SelectionSeed = 0,
    int? Variant = null);
