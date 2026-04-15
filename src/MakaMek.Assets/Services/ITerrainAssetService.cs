using Sanet.MakaMek.Assets.Models.Terrains;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Service for caching and retrieving terrain assets from MMTX packages
/// </summary>
public interface ITerrainAssetService
{
    /// <summary>
    /// Gets the manifest for a loaded biome.
    /// Ensures provider-backed biomes are initialized before returning.
    /// </summary>
    /// <param name="biomeId">The biome identifier</param>
    /// <returns>Biomes manifest if loaded, null otherwise</returns>
    Task<BiomeManifest?> GetBiomeManifest(string biomeId);

    /// <summary>
    /// Gets all loaded biome identifiers.
    /// Ensures provider-backed biomes are initialized before returning.
    /// </summary>
    /// <returns>Collection of biome IDs</returns>
    Task<IEnumerable<string>> GetLoadedBiomes();
    
    /// <summary>
    /// Gets a base terrain image for the specified biome
    /// </summary>
    /// <param name="biomeId">The biome identifier</param>
    /// <param name="variant">Optional variant number (randomly selected if not specified)</param>
    /// <returns>Image bytes if found, null otherwise</returns>
    Task<byte[]?> GetBaseBiomeImage(string biomeId, int? variant = null);
    
    /// <summary>
    /// Gets a terrain overlay image for the specified terrain type
    /// </summary>
    /// <param name="biomeId">The biome identifier</param>
    /// <param name="terrainType">Terrain type name (e.g., "lightwoods", "heavywoods")</param>
    /// <param name="variant">Optional variant number</param>
    /// <returns>Image bytes if found, null otherwise</returns>
    Task<byte[]?> GetTerrainOverlayImage(string biomeId, string terrainType, int? variant = null);
    
    /// <summary>
    /// Gets an edge effect image for the specified direction and type
    /// </summary>
    /// <param name="biomeId">The biome identifier</param>
    /// <param name="direction">Hex edge direction (0-5)</param>
    /// <param name="edgeType">Edge type (top or bottom)</param>
    /// <param name="coordinates">Hex coordinates for deterministic variant selection</param>
    /// <returns>Image bytes if found, null otherwise</returns>
    Task<byte[]?> GetEdgeImage(string biomeId, HexDirection direction, TerrainAssetType edgeType, HexCoordinates coordinates);
    
    /// <summary>
    /// Gets available variants for a specific asset type.
    /// Ensures provider-backed biomes are initialized before returning.
    /// </summary>
    /// <param name="biomeId">The biome identifier</param>
    /// <param name="assetType">Type of asset</param>
    /// <param name="assetName">Name of the asset (terrain type or direction for edges)</param>
    /// <returns>List of available variant numbers</returns>
    Task<IReadOnlyList<int>> GetAvailableVariants(string biomeId, TerrainAssetType assetType, string assetName);
    
    /// <summary>
    /// Gets a water bitmask texture image for the specified canonical bitmask.
    /// Files are stored in the <c>terrains/water/</c> folder of MMTX packages
    /// and named using the 6-bit binary representation of <see cref="CanonicalBitmaskResult.CanonicalMask"/>,
    /// e.g. <c>000001.png</c> or <c>000001-1.png</c> for variant 1.
    /// </summary>
    /// <param name="biomeId">The biome identifier</param>
    /// <param name="canonicalBitmask">The canonical bitmask result containing the mask and rotation</param>
    /// <returns>Image bytes if found, null otherwise</returns>
    Task<byte[]?> GetWaterTextureImage(string biomeId, CanonicalBitmaskResult canonicalBitmask);

    /// <summary>
    /// Loads a terrain biome from an MMTX package stream
    /// </summary>
    /// <param name="mmtxStream">Stream containing the MMTX package data</param>
    /// <returns>The loaded biome manifest</returns>
    Task<BiomeManifest?> LoadTerrainFromMmtxStreamAsync(Stream mmtxStream);
    
    /// <summary>
    /// Clears all cached terrain data
    /// </summary>
    void ClearCache();
}
