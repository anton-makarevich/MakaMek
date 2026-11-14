using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Sanet.MakaMek.Assets.Models.Terrains;
using Sanet.MakaMek.Assets.Services.PackageReaders;

namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Bookkeeping for the terrain cache: cache/variant keys, duplicate-package replacement,
/// deterministic variant selection and image lookups. Stateless — all data lives in the
/// <see cref="TerrainCachingService.TerrainCacheState"/> passed in by the caller, so a single
/// instance is safe to share between the caching service and the package loader.
/// </summary>
public sealed class TerrainAssetCache
{
    /// <summary>
    /// Looks up the image described by <paramref name="request"/>, selecting a variant
    /// deterministically when the request does not name one explicitly.
    /// </summary>
    /// <returns>The cached image, or <c>null</c> when the biome has no such asset</returns>
    public byte[]? GetImage(
        TerrainCachingService.TerrainCacheState state,
        string biomeId,
        TerrainImageRequest request)
    {
        var variants = GetVariants(state, biomeId, request.AssetType, request.AssetName);
        if (variants.Count == 0) return null;

        var selectedVariant = request.Variant
                              ?? SelectVariant(variants, biomeId, request.AssetName, request.SelectionSeed);
        var cacheKey = GetCacheKey(biomeId, request.AssetType, request.AssetName, selectedVariant);

        return state.ImageCache.GetValueOrDefault(cacheKey);
    }

    /// <summary>
    /// Gets the available variant numbers for an asset of the given biome.
    /// </summary>
    public IReadOnlyList<int> GetVariants(
        TerrainCachingService.TerrainCacheState state,
        string biomeId,
        TerrainAssetType assetType,
        string assetName)
    {
        var variantKey = GetVariantKey(biomeId, assetType, assetName);
        return state.VariantCache.TryGetValue(variantKey, out var variants)
            ? variants
            : Array.Empty<int>();
    }

    /// <summary>
    /// Adds a parsed terrain package (manifest + assets) to the cache, applying the shared
    /// duplicate policy: a package from a provider lower in the list overwrites an earlier one.
    /// </summary>
    /// <param name="state">Cache state to merge into</param>
    /// <param name="package">Parsed package to merge</param>
    /// <param name="providerId">Provider the package came from, or <c>null</c> for direct stream loads</param>
    /// <param name="onDuplicateAsset">
    /// Invoked with the cache key when an image overwrites an already cached one; lets the
    /// owning service report the conflict with its own logger
    /// </param>
    /// <returns><c>true</c> when a previously cached biome with the same id was replaced</returns>
    public bool AddPackage(
        TerrainCachingService.TerrainCacheState state,
        TerrainPackage package,
        string? providerId,
        Action<string>? onDuplicateAsset = null)
    {
        // Serialize the entire merge — duplicate detection, remove and all inserts — so a
        // duplicate biome is replaced by one complete package instead of an interleaved
        // mixture when multiple callers merge into the same state concurrently.
        lock (state.SyncRoot)
        {
            var replacedExistingBiome = state.BiomeManifests.ContainsKey(package.Manifest.Id);
            if (replacedExistingBiome)
            {
                RemoveBiomeAssets(state, package.Manifest.Id);
            }

            state.BiomeManifests[package.Manifest.Id] = package.Manifest;
            if (!string.IsNullOrEmpty(providerId))
                state.ResourceOwnership[package.Manifest.Id] = providerId;
            else
                state.ResourceOwnership.TryRemove(package.Manifest.Id, out _);

            foreach (var asset in package.Assets)
            {
                var cacheKey = GetCacheKey(package.Manifest.Id, asset.AssetType, asset.AssetName, asset.Variant);
                AddImage(state, cacheKey, asset.Image, onDuplicateAsset);

                // Track variants
                var variantKey = GetVariantKey(package.Manifest.Id, asset.AssetType, asset.AssetName);
                state.VariantCache.AddOrUpdate(
                    variantKey,
                    _ => ImmutableSortedSet.Create(asset.Variant),
                    (_, set) => set.Add(asset.Variant));
            }

            return replacedExistingBiome;
        }
    }

    private static void AddImage(
        TerrainCachingService.TerrainCacheState state,
        string cacheKey,
        byte[] image,
        Action<string>? onDuplicateAsset)
    {
        if (state.ImageCache.TryAdd(cacheKey, image)) return;

        onDuplicateAsset?.Invoke(cacheKey);
        state.ImageCache[cacheKey] = image;
    }

    private static void RemoveBiomeAssets(TerrainCachingService.TerrainCacheState state, string biomeId)
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
    private static int SelectVariant(IReadOnlyList<int> variants, string biomeId, string assetName, int seed)
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
