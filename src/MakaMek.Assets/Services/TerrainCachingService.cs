using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Models.Terrains;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Assets.ResourceProviders;

namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Service for caching and retrieving terrain assets from MMTX packages
/// </summary>
public class TerrainCachingService : ITerrainAssetService
{
    private IReadOnlyList<IResourceStreamProvider> _streamProviders;
    private readonly ILogger<TerrainCachingService> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    
    /// <summary>
    /// Immutable-by-publication snapshot of all cached data. A new instance is built
    /// completely and then published via a single volatile write to <see cref="_state"/>,
    /// so readers either observe the previous complete cache or the new complete cache,
    /// never a cleared or partially rebuilt state.
    /// </summary>
    private sealed class CacheState
    {
        public readonly ConcurrentDictionary<string, BiomeManifest> BiomeManifests = new();
        public readonly ConcurrentDictionary<string, byte[]> ImageCache = new();
        public readonly ConcurrentDictionary<string, ImmutableSortedSet<int>> VariantCache = new();
        public volatile bool IsInitialized;
    }

    private volatile CacheState _state = new();

    public event EventHandler<ResourceLoadProgressEventArgs>? LoadProgress;

    public TerrainCachingService(
        IEnumerable<IResourceStreamProvider> streamProviders,
        ILoggerFactory loggerFactory)
    {
        _streamProviders = [.. streamProviders];
        _logger = loggerFactory.CreateLogger<TerrainCachingService>();
    }

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

    private static IReadOnlyList<int> GetAvailableVariants(CacheState state, string biomeId, TerrainAssetType assetType, string assetName)
    {
        var variantKey = GetVariantKey(biomeId, assetType, assetName);
        return state.VariantCache.TryGetValue(variantKey, out var variants)
            ? variants
            : Array.Empty<int>();
    }

    /// <inheritdoc />
    public async Task<BiomeManifest?> LoadTerrainFromMmtxStream(Stream mmtxStream)
    {
        return await LoadTerrainFromMmtxStream(mmtxStream, _state);
    }

    private async Task<BiomeManifest?> LoadTerrainFromMmtxStream(Stream mmtxStream, CacheState state)
    {
        try
        {
            await using var archive = new ZipArchive(mmtxStream, ZipArchiveMode.Read);
            
            // Load manifest.json
            var manifestEntry = archive.GetEntry("manifest.json");
            if (manifestEntry == null)
            {
                _logger.LogWarning("MMTX package missing manifest.json");
                return null;
            }

            BiomeManifest manifest;
            await using (var manifestStream = await manifestEntry.OpenAsync())
            using (var reader = new StreamReader(manifestStream))
            {
                var jsonContent = await reader.ReadToEndAsync();
                manifest = JsonSerializer.Deserialize<BiomeManifest>(jsonContent, _jsonOptions)
                    ?? throw new InvalidOperationException("Failed to deserialize manifest.json");
            }
            
            if (string.IsNullOrEmpty(manifest.Id))
            {
                _logger.LogWarning("MMTX package manifest missing id");
                return null;
            }

            // Cache the manifest first to reject duplicates before extracting images
            if (!state.BiomeManifests.TryAdd(manifest.Id, manifest))
            {
                _logger.LogWarning("Duplicate biome ID '{BiomeId}' found, skipping extraction", manifest.Id);
                return null;
            }

            // Extract and cache all images (only if manifest was successfully added)
            await ExtractImagesAsync(archive, manifest.Id, state);
            
            _logger.LogInformation("Loaded terrain biome '{BiomeId}' version {Version}", 
                manifest.Id, manifest.Version);
            
            return manifest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading MMTX package");
            return null;
        }
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        _initializationLock.Wait();
        try
        {
            ClearCacheLocked();
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <inheritdoc />
    public void SetProviders(IEnumerable<IResourceStreamProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _initializationLock.Wait();
        try
        {
            _streamProviders = providers.ToList();
            ClearCacheLocked();
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task ReloadProviders()
    {
        await _initializationLock.WaitAsync();
        try
        {
            // Build the replacement state completely before publishing it, so readers
            // keep seeing the previous complete cache until the swap.
            var freshState = new CacheState();
            await LoadTerrainFromStreamProviders(freshState);
            freshState.IsInitialized = true;
            _state = freshState;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <summary>
    /// Replaces the cached state with an empty, uninitialized one. Readers already
    /// holding the previous state continue to see complete data; new readers wait
    /// for re-initialization. Must be called while holding <see cref="_initializationLock"/>.
    /// </summary>
    private void ClearCacheLocked()
    {
        _state = new CacheState();
    }

    private async Task<CacheState> EnsureInitialized()
    {
        var state = _state;
        if (state.IsInitialized) return state;

        await _initializationLock.WaitAsync();
        try
        {
            state = _state;
            if (state.IsInitialized) return state; // double-check after acquiring a lock
            await LoadTerrainFromStreamProviders(state);
            state.IsInitialized = true;
            return state;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task LoadTerrainFromStreamProviders(CacheState state)
    {
        // Enumerate all providers up front so the total is finalized before loading begins.
        // This keeps TotalCount stable and ensures reported progress cannot decrease between providers.
        var resources = new List<(IResourceStreamProvider Provider, string ResourceId)>();
        foreach (var provider in _streamProviders)
        {
            try
            {
                var resourceIds = await provider.GetAvailableResourceIds();
                foreach (var resourceId in resourceIds)
                {
                    resources.Add((provider, resourceId));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading terrain from provider {ProviderType}", 
                    provider.GetType().Name);
            }
        }

        var totalResources = resources.Count;
        var processedResources = 0;
        RaiseLoadProgress(processedResources, totalResources);

        foreach (var (provider, resourceId) in resources)
        {
            try
            {
                await using var stream = await provider.GetResourceStream(resourceId);
                if (stream != null)
                {
                    await LoadTerrainFromMmtxStream(stream, state);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading terrain from '{ResourceId}'", resourceId);
            }
            processedResources++;
            RaiseLoadProgress(processedResources, totalResources);
        }
    }

    private void RaiseLoadProgress(int loadedCount, int totalCount)
    {
        LoadProgress?.Invoke(this, new ResourceLoadProgressEventArgs(loadedCount, totalCount));
    }

    private async Task ExtractImagesAsync(ZipArchive archive, string biomeId, CacheState state)
    {
        // Extract base terrain images
        await ExtractImagesFromDirectoryAsync(archive, biomeId, "", TerrainAssetType.Base, state);

        // Extract terrain overlay images
        await ExtractImagesFromDirectoryAsync(archive, biomeId, "terrains/", TerrainAssetType.Overlay, state);

        // Extract water bitmask textures from terrains/water/
        await ExtractImagesFromDirectoryAsync(archive, biomeId, "terrains/water/", TerrainAssetType.Water, state);

        // Extract road bitmask textures from terrains/road/
        await ExtractImagesFromDirectoryAsync(archive, biomeId, "terrains/road/", TerrainAssetType.Road, state);

        // Extract edge images
        await ExtractEdgeImagesAsync(archive, biomeId, state);
    }

    private async Task ExtractImagesFromDirectoryAsync(
        ZipArchive archive,
        string biomeId,
        string directory,
        TerrainAssetType assetType,
        CacheState state)
    {
        var entries = archive.Entries
            .Where(e => e.FullName.StartsWith(directory, StringComparison.OrdinalIgnoreCase) &&
                        e.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                        e.FullName.IndexOf('/', directory.Length) == -1)
            .ToList();

        foreach (var entry in entries)
        {
            var fileName = Path.GetFileNameWithoutExtension(entry.Name);
            var parsed = ParseAssetFileName(fileName);
            if (parsed == null) continue;

            await using var stream = await entry.OpenAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);

            var cacheKey = GetCacheKey(biomeId, assetType, parsed.AssetName, parsed.Variant);
            state.ImageCache.TryAdd(cacheKey, memoryStream.ToArray());

            // Track variants
            var variantKey = GetVariantKey(biomeId, assetType, parsed.AssetName);
            state.VariantCache.AddOrUpdate(
                variantKey,
                _ => ImmutableSortedSet.Create(parsed.Variant),
                (_, set) => set.Add(parsed.Variant));
        }
    }

    private async Task ExtractEdgeImagesAsync(ZipArchive archive, string biomeId, CacheState state)
    {
        const string edgesDirectory = "edges/";
        var edgeEntries = archive.Entries
            .Where(e => e.FullName.StartsWith(edgesDirectory, StringComparison.OrdinalIgnoreCase) &&
                        e.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var entry in edgeEntries)
        {
            var fileName = Path.GetFileNameWithoutExtension(entry.Name);
            var parsed = ParseEdgeFileName(fileName);
            if (parsed == null) continue;

            var assetType = parsed.EdgeType == "top" ? TerrainAssetType.EdgeTop : TerrainAssetType.EdgeBottom;

            await using var stream = await entry.OpenAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);

            var cacheKey = GetCacheKey(biomeId, assetType, parsed.Direction, parsed.Variant);
            state.ImageCache.TryAdd(cacheKey, memoryStream.ToArray());

            // Track variants
            var variantKey = GetVariantKey(biomeId, assetType, parsed.Direction);
            state.VariantCache.AddOrUpdate(
                variantKey,
                _ => ImmutableSortedSet.Create(parsed.Variant),
                (_, set) => set.Add(parsed.Variant));
        }
    }

    private record AssetInfo(string AssetName, int Variant);
    private record EdgeInfo(string EdgeType, string Direction, int Variant);

    /// <summary>
    /// Parses an asset file name into asset name and zero-based variant number.
    /// Returns null when the file has an invalid (non-integer) variant suffix so the asset is skipped.
    /// Examples: "base" -> ("base", 0), "base-1" -> ("base", 1), "base-abc" -> null
    /// </summary>
    private static AssetInfo? ParseAssetFileName(string fileName)
    {
        var normalizedFileName = fileName.ToLowerInvariant();
        var lastDashIndex = fileName.LastIndexOf('-');
        if (lastDashIndex < 0)
            return new AssetInfo(normalizedFileName, 0);

        var namePart = fileName[..lastDashIndex];
        var variantPart = fileName[(lastDashIndex + 1)..];

        return TryParseVariantSuffix(variantPart, out var variant)
            ? new AssetInfo(namePart.ToLowerInvariant(), variant)
            : null;
    }

    /// <summary>
    /// Parses an edge file name into edge type, direction, and zero-based variant number.
    /// Returns null when the file name is invalid or has an invalid variant suffix so the asset is skipped.
    /// Examples: "top-0" -> ("top", "0", 0), "top-0-1" -> ("top", "0", 1), "top-0-abc" -> null
    /// </summary>
    private static EdgeInfo? ParseEdgeFileName(string fileName)
    {
        var parts = fileName.Split('-');

        if (parts.Length is < 2 or > 3)
            return null;

        var edgeType = parts[0].ToLowerInvariant();
        if (edgeType is not ("top" or "bottom"))
            return null;

        var direction = parts[1];
        if (!int.TryParse(direction, out _))
            return null;

        if (parts.Length == 2)
            return new EdgeInfo(edgeType, direction, 0);

        return TryParseVariantSuffix(parts[2], out var variant)
            ? new EdgeInfo(edgeType, direction, variant)
            : null;
    }

    private static bool TryParseVariantSuffix(string variantPart, out int variant)
    {
        variant = 0;
        if (!int.TryParse(variantPart, out var variantNum) || variantNum <= 0)
            return false;

        variant = variantNum;
        return true;
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
