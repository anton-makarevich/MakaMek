using Sanet.MakaMek.Assets.Models.Terrains;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Service for caching and retrieving terrain assets from MMTX packages
/// </summary>
public interface ITerrainAssetService
{
    /// <summary>
    /// Gets the manifest for a loaded biome
    /// </summary>
    /// <param name="biomeId">The biome identifier</param>
    /// <returns>Biomes manifest if loaded, null otherwise</returns>
    BiomeManifest? GetBiomeManifest(string biomeId);
    
    /// <summary>
    /// Gets all loaded biome identifiers
    /// </summary>
    /// <returns>Collection of biome IDs</returns>
    IEnumerable<string> GetLoadedBiomes();
    
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
    /// Gets available variants for a specific asset type
    /// </summary>
    /// <param name="biomeId">The biome identifier</param>
    /// <param name="assetType">Type of asset</param>
    /// <param name="assetName">Name of the asset (terrain type or direction for edges)</param>
    /// <returns>List of available variant numbers</returns>
    IReadOnlyList<int> GetAvailableVariants(string biomeId, TerrainAssetType assetType, string assetName);
    
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
