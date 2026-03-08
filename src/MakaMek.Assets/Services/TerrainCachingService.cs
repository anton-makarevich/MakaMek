using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Models.Terrains;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Service for caching and retrieving terrain assets from MMTX packages
/// </summary>
public class TerrainCachingService : ITerrainAssetService
{
    private readonly ConcurrentDictionary<string, BiomeManifest> _biomeManifests = new();
    private readonly ConcurrentDictionary<string, byte[]> _imageCache = new();
    private readonly ConcurrentDictionary<string, ImmutableSortedSet<int>> _variantCache = new();
    private readonly IEnumerable<IResourceStreamProvider> _streamProviders;
    private readonly ILogger<TerrainCachingService> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    
    private volatile bool _isInitialized;

    public TerrainCachingService(
        IEnumerable<IResourceStreamProvider> streamProviders,
        ILoggerFactory loggerFactory)
    {
        _streamProviders = streamProviders;
        _logger = loggerFactory.CreateLogger<TerrainCachingService>();
    }

    /// <inheritdoc />
    public BiomeManifest? GetBiomeManifest(string biomeId)
    {
        return _biomeManifests.GetValueOrDefault(biomeId);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetLoadedBiomes()
    {
        return _biomeManifests.Keys;
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetBaseBiomeImage(string biomeId, int? variant = null)
    {
        await EnsureInitialized();
        
        var variants = GetAvailableVariants(biomeId, TerrainAssetType.Base, "base");
        if (variants.Count == 0) return null;
        
        var selectedVariant = variant ?? SelectRandomVariant(variants, biomeId, "base", 0);
        var cacheKey = GetCacheKey(biomeId, TerrainAssetType.Base, "base", selectedVariant);
        
        return _imageCache.GetValueOrDefault(cacheKey);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetTerrainOverlayImage(string biomeId, string terrainType, int? variant = null)
    {
        await EnsureInitialized();
        
        var variants = GetAvailableVariants(biomeId, TerrainAssetType.Overlay, terrainType);
        if (variants.Count == 0) return null;
        
        var selectedVariant = variant ?? SelectRandomVariant(variants, biomeId, terrainType, 0);
        var cacheKey = GetCacheKey(biomeId, TerrainAssetType.Overlay, terrainType, selectedVariant);
        
        return _imageCache.GetValueOrDefault(cacheKey);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetEdgeImage(string biomeId, HexDirection direction, TerrainAssetType edgeType, HexCoordinates coordinates)
    {
        await EnsureInitialized();
        
        if (edgeType is not (TerrainAssetType.EdgeTop or TerrainAssetType.EdgeBottom))
            return null;
        
        var directionName = ((int)direction).ToString();
        var variants = GetAvailableVariants(biomeId, edgeType, directionName);
        if (variants.Count == 0) return null;
        
        // Use hex coordinates for deterministic variant selection
        var selectedVariant = SelectRandomVariant(variants, biomeId, directionName, coordinates.Q + coordinates.R * 31);
        var cacheKey = GetCacheKey(biomeId, edgeType, directionName, selectedVariant);
        
        return _imageCache.GetValueOrDefault(cacheKey);
    }

    /// <inheritdoc />
    public IReadOnlyList<int> GetAvailableVariants(string biomeId, TerrainAssetType assetType, string assetName)
    {
        var variantKey = GetVariantKey(biomeId, assetType, assetName);
        return _variantCache.TryGetValue(variantKey, out var variants) 
            ? variants 
            : Array.Empty<int>();
    }

    /// <inheritdoc />
    public async Task<BiomeManifest?> LoadTerrainFromMmtxStreamAsync(Stream mmtxStream)
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

            // Extract and cache all images
            await ExtractImagesAsync(archive, manifest.Id);
            
            // Cache the manifest
            _biomeManifests.TryAdd(manifest.Id, manifest);
            
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
        _biomeManifests.Clear();
        _imageCache.Clear();
        _variantCache.Clear();
        _isInitialized = false;
    }

    private async Task EnsureInitialized()
    {
        if (_isInitialized) return;

        await _initializationLock.WaitAsync();
        try
        {
            if (_isInitialized) return; // double-check after acquiring a lock
            await LoadTerrainFromStreamProviders();
            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task LoadTerrainFromStreamProviders()
    {
        foreach (var provider in _streamProviders)
        {
            try
            {
                var resourceIds = await provider.GetAvailableResourceIds();
                
                foreach (var resourceId in resourceIds)
                {
                    try
                    {
                        await using var stream = await provider.GetResourceStream(resourceId);
                        if (stream != null)
                        {
                            await LoadTerrainFromMmtxStreamAsync(stream);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading terrain from '{ResourceId}'", resourceId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading terrain from provider {ProviderType}", 
                    provider.GetType().Name);
            }
        }
    }

    private async Task ExtractImagesAsync(ZipArchive archive, string biomeId)
    {
        // Extract base terrain images
        await ExtractImagesFromDirectoryAsync(archive, biomeId, "", TerrainAssetType.Base);
        
        // Extract terrain overlay images
        await ExtractImagesFromDirectoryAsync(archive, biomeId, "terrains/", TerrainAssetType.Overlay);
        
        // Extract edge images
        await ExtractEdgeImagesAsync(archive, biomeId);
    }

    private async Task ExtractImagesFromDirectoryAsync(
        ZipArchive archive, 
        string biomeId, 
        string directory,
        TerrainAssetType assetType)
    {
        var entries = archive.Entries
            .Where(e => e.FullName.StartsWith(directory, StringComparison.OrdinalIgnoreCase) &&
                        e.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                        e.FullName.IndexOf('/', directory.Length) == -1)
            .ToList();

        foreach (var entry in entries)
        {
            var fileName = Path.GetFileNameWithoutExtension(entry.Name);
            var (assetName, variant) = ParseAssetFileName(fileName);
            
            await using var stream = await entry.OpenAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            
            var cacheKey = GetCacheKey(biomeId, assetType, assetName, variant);
            _imageCache.TryAdd(cacheKey, memoryStream.ToArray());
            
            // Track variants
            var variantKey = GetVariantKey(biomeId, assetType, assetName);
            _variantCache.AddOrUpdate(
                variantKey,
                _ => ImmutableSortedSet.Create(variant),
                (_, set) => set.Add(variant));
        }
    }

    private async Task ExtractEdgeImagesAsync(ZipArchive archive, string biomeId)
    {
        const string edgesDirectory = "edges/";
        var edgeEntries = archive.Entries
            .Where(e => e.FullName.StartsWith(edgesDirectory, StringComparison.OrdinalIgnoreCase) &&
                        e.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var entry in edgeEntries)
        {
            var fileName = Path.GetFileNameWithoutExtension(entry.Name);
            var (edgeType, direction, variant) = ParseEdgeFileName(fileName);
            
            if (edgeType == null) continue;
            
            var assetType = edgeType == "top" ? TerrainAssetType.EdgeTop : TerrainAssetType.EdgeBottom;
            
            await using var stream = await entry.OpenAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            
            var cacheKey = GetCacheKey(biomeId, assetType, direction, variant);
            _imageCache.TryAdd(cacheKey, memoryStream.ToArray());
            
            // Track variants
            var variantKey = GetVariantKey(biomeId, assetType, direction);
            _variantCache.AddOrUpdate(
                variantKey,
                _ => ImmutableSortedSet.Create(variant),
                (_, set) => set.Add(variant));
        }
    }

    /// <summary>
    /// Parses an asset file name into asset name and zero-based variant number.
    /// Examples: "base" -> ("base", 0), "base-1" -> ("base", 1), "lightwoods-2" -> ("lightwoods", 2)
    /// </summary>
    private static (string assetName, int variant) ParseAssetFileName(string fileName)
    {
        var normalizedFileName = fileName.ToLowerInvariant();
        var lastDashIndex = fileName.LastIndexOf('-');
        if (lastDashIndex < 0)
            return (normalizedFileName, 0);
        
        var namePart = fileName[..lastDashIndex];
        var variantPart = fileName[(lastDashIndex + 1)..];
        
        return TryParseVariantSuffix(variantPart, out var variant)
            ? (namePart.ToLowerInvariant(), variant)
            : (normalizedFileName, 0);
    }

    /// <summary>
    /// Parses an edge file name into edge type, direction, and zero-based variant number.
    /// Examples: "top-0" -> ("top", "0", 0), "top-0-1" -> ("top", "0", 1), "bottom-5-3" -> ("bottom", "5", 3)
    /// </summary>
    private static (string? edgeType, string direction, int variant) ParseEdgeFileName(string fileName)
    {
        var parts = fileName.Split('-');
        
        if (parts.Length is < 2 or > 3)
            return (null, "0", 0);
        
        var edgeType = parts[0].ToLowerInvariant();
        if (edgeType is not ("top" or "bottom"))
            return (null, "0", 0);
        
        var direction = parts[1];
        if (!int.TryParse(direction, out _))
            return (null, "0", 0);
        
        if (parts.Length == 2)
            return (edgeType, direction, 0);

        return TryParseVariantSuffix(parts[2], out var variant)
            ? (edgeType, direction, variant)
            : (null, "0", 0);
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
