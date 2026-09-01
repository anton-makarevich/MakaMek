using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Models.Terrains;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Assets.ResourceProviders;
using Sanet.MakaMek.Assets.Services.PackageReaders;

namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Service for caching and retrieving terrain assets from MMTX packages
/// </summary>
public class TerrainCachingService : PackageCacheCore<TerrainCachingService.TerrainCacheState>, ITerrainAssetService
{
    private readonly ILogger<TerrainCachingService> _logger;
    private readonly MmtxTerrainPackageReader _packageReader = new();

    /// <summary>
    /// Snapshot of all cached terrain data.
    /// </summary>
    public sealed class TerrainCacheState : PackageCacheState
    {
        /// <summary>
        /// Synchronizes package-merge operations so a duplicate biome is replaced by one complete
        /// package rather than an interleaved mixture from concurrent callers on this same state.
        /// </summary>
        public readonly object SyncRoot = new();

        public readonly ConcurrentDictionary<string, BiomeManifest> BiomeManifests = new();
        public readonly ConcurrentDictionary<string, byte[]> ImageCache = new();
        public readonly ConcurrentDictionary<string, ImmutableSortedSet<int>> VariantCache = new();
    }

    public TerrainCachingService(
        IEnumerable<IResourceStreamProvider> streamProviders,
        ILoggerFactory loggerFactory)
        : base(streamProviders)
    {
        _logger = loggerFactory.CreateLogger<TerrainCachingService>();
    }

    /// <inheritdoc />
    protected override string ResourceKind => "terrain biome";

    /// <inheritdoc />
    protected override ILogger Logger => _logger;

    /// <inheritdoc />
    public async Task<BiomeManifest?> GetBiomeManifest(string biomeId)
    {
        var state = await EnsureInitialized();
        return state.BiomeManifests.GetValueOrDefault(biomeId);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetLoadedBiomes()
    {
        var state = await EnsureInitialized();
        return state.BiomeManifests.Keys;
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetBaseBiomeImage(string biomeId, int? variant = null)
    {
        var state = await EnsureInitialized();

        var variants = GetAvailableVariants(state, biomeId, TerrainAssetType.Base, "base");
        if (variants.Count == 0) return null;

        var selectedVariant = variant ?? SelectRandomVariant(variants, biomeId, "base", 0);
        var cacheKey = GetCacheKey(biomeId, TerrainAssetType.Base, "base", selectedVariant);

        return state.ImageCache.GetValueOrDefault(cacheKey);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetTerrainOverlayImage(string biomeId, string terrainType, int? variant = null)
    {
        var state = await EnsureInitialized();

        var variants = GetAvailableVariants(state, biomeId, TerrainAssetType.Overlay, terrainType);
        if (variants.Count == 0) return null;

        var selectedVariant = variant ?? SelectRandomVariant(variants, biomeId, terrainType, 0);
        var cacheKey = GetCacheKey(biomeId, TerrainAssetType.Overlay, terrainType, selectedVariant);

        return state.ImageCache.GetValueOrDefault(cacheKey);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetEdgeImage(string biomeId, HexDirection direction, TerrainAssetType edgeType, HexCoordinates coordinates)
    {
        var state = await EnsureInitialized();

        if (edgeType is not (TerrainAssetType.EdgeTop or TerrainAssetType.EdgeBottom))
            return null;

        var directionName = ((int)direction).ToString();
        var variants = GetAvailableVariants(state, biomeId, edgeType, directionName);
        if (variants.Count == 0) return null;

        // Use hex coordinates for deterministic variant selection
        var selectedVariant = SelectRandomVariant(variants, biomeId, directionName, coordinates.Q + coordinates.R * 31);
        var cacheKey = GetCacheKey(biomeId, edgeType, directionName, selectedVariant);

        return state.ImageCache.GetValueOrDefault(cacheKey);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetWaterTextureImage(string biomeId, CanonicalBitmaskResult canonicalBitmask)
    {
        return await GetBitmaskTexture(biomeId, canonicalBitmask, TerrainAssetType.Water);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetRoadTextureImage(string biomeId, CanonicalBitmaskResult canonicalBitmask)
    {
        return await GetBitmaskTexture(biomeId, canonicalBitmask, TerrainAssetType.Road);
    }

    private async Task<byte[]?> GetBitmaskTexture(string biomeId, CanonicalBitmaskResult canonicalBitmask, TerrainAssetType assetType)
    {
        var state = await EnsureInitialized();

        // Convert canonical mask to 6-digit binary string (e.g., mask 1 → "000001")
        var bitmaskName = Convert.ToString(canonicalBitmask.CanonicalMask, 2).PadLeft(6, '0');

        var variants = GetAvailableVariants(state, biomeId, assetType, bitmaskName);
        if (variants.Count == 0) return null;

        var selectedVariant = SelectRandomVariant(variants, biomeId, bitmaskName, 0);
        var cacheKey = GetCacheKey(biomeId, assetType, bitmaskName, selectedVariant);

        return state.ImageCache.GetValueOrDefault(cacheKey);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<int>> GetAvailableVariants(string biomeId, TerrainAssetType assetType, string assetName)
    {
        var state = await EnsureInitialized();
        return GetAvailableVariants(state, biomeId, assetType, assetName);
    }

    private static IReadOnlyList<int> GetAvailableVariants(TerrainCacheState state, string biomeId, TerrainAssetType assetType, string assetName)
    {
        var variantKey = GetVariantKey(biomeId, assetType, assetName);
        return state.VariantCache.TryGetValue(variantKey, out var variants)
            ? variants
            : Array.Empty<int>();
    }

    /// <inheritdoc />
    public async Task<BiomeManifest?> LoadTerrainFromMmtxStream(Stream mmtxStream)
    {
        try
        {
            var package = await _packageReader.Read(mmtxStream);
            AddPackageToCache(package, CurrentState);
            return package.Manifest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading MMTX package");
            return null;
        }
    }

    /// <inheritdoc />
    protected override async Task LoadResource(
        IResourceStreamProvider provider,
        string resourceId,
        Stream stream,
        TerrainCacheState state,
        CancellationToken cancellationToken = default)
    {
        var package = await _packageReader.Read(stream, cancellationToken);
        AddPackageToCache(package, state);
        _logger.LogInformation("Loaded terrain biome '{BiomeId}' version {Version}",
            package.Manifest.Id, package.Manifest.Version);
    }

    /// <summary>
    /// Adds a parsed terrain package (manifest + assets) to the cache, applying the shared
    /// duplicate policy: a package from a provider lower in the list overwrites an earlier one.
    /// </summary>
    private void AddPackageToCache(TerrainPackage package, TerrainCacheState state)
    {
        // Serialize the entire merge — duplicate detection, remove and all inserts — so a
        // duplicate biome is replaced by one complete package instead of an interleaved
        // mixture when multiple callers merge into the same state concurrently.
        lock (state.SyncRoot)
        {
            // Apply the shared duplicate policy: a package from a provider lower in the list
            // replaces the earlier one entirely, so remove the previous biome's assets first.
            if (state.BiomeManifests.ContainsKey(package.Manifest.Id))
            {
                _logger.LogWarning("Duplicate terrain biome '{BiomeId}' found; replacing previously loaded assets",
                    package.Manifest.Id);
                RemoveBiomeAssets(state, package.Manifest.Id);
            }

            state.BiomeManifests[package.Manifest.Id] = package.Manifest;

            foreach (var asset in package.Assets)
            {
                var cacheKey = GetCacheKey(package.Manifest.Id, asset.AssetType, asset.AssetName, asset.Variant);
                TryCache(state.ImageCache, cacheKey, asset.Image);

                // Track variants
                var variantKey = GetVariantKey(package.Manifest.Id, asset.AssetType, asset.AssetName);
                state.VariantCache.AddOrUpdate(
                    variantKey,
                    _ => ImmutableSortedSet.Create(asset.Variant),
                    (_, set) => set.Add(asset.Variant));
            }
        }
    }

    private static void RemoveBiomeAssets(TerrainCacheState state, string biomeId)
    {
        var prefix = $"{biomeId}/";
        foreach (var key in state.ImageCache.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            state.ImageCache.TryRemove(key, out _);
        }
        foreach (var key in state.VariantCache.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            state.VariantCache.TryRemove(key, out _);
        }
    }

    private static string GetCacheKey(string biomeId, TerrainAssetType assetType, string assetName, int variant)
    {
        return $"{biomeId}/{assetType}/{assetName}/{variant}";
    }

    private static string GetVariantKey(string biomeId, TerrainAssetType assetType, string assetName)
    {
        return $"{biomeId}/{assetType}/{assetName}";
    }

    /// <summary>
    /// Selects a variant deterministically based on a seed value
    /// Uses hash-based selection for consistent results across sessions
    /// </summary>
    private static int SelectRandomVariant(IReadOnlyList<int> variants, string biomeId, string assetName, int seed)
    {
        if (variants.Count == 0) return 0;
        if (variants.Count == 1) return variants[0];

        // Combine biome, asset name, and seed for deterministic selection
        var combined = $"{biomeId}-{assetName}-{seed}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        var hashValue = BitConverter.ToUInt32(hash, 0);

        var index = (int)(hashValue % (uint)variants.Count);
        return variants[index];
    }
}