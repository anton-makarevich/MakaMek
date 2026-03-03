using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Data.Terrain;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Service for caching and retrieving terrain assets from MMTX packages
/// </summary>
public class TerrainCachingService : ITerrainAssetService
{
    private readonly ConcurrentDictionary<string, TerrainThemeManifest> _themeManifests = new();
    private readonly ConcurrentDictionary<string, byte[]> _imageCache = new();
    private readonly ConcurrentDictionary<string, ImmutableSortedSet<int>> _variantCache = new();
    private readonly IEnumerable<IResourceStreamProvider> _streamProviders;
    private readonly ILogger<TerrainCachingService> _logger;
    private readonly Lock _initializationLock = new();
    
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
    public TerrainThemeManifest? GetThemeManifest(string themeId)
    {
        return _themeManifests.GetValueOrDefault(themeId);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetLoadedThemes()
    {
        return _themeManifests.Keys;
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetBaseTerrainImage(string themeId, int? variant = null)
    {
        await EnsureInitialized();
        
        var variants = GetAvailableVariants(themeId, TerrainAssetType.Base, "base");
        if (variants.Count == 0) return null;
        
        var selectedVariant = variant ?? SelectRandomVariant(variants, themeId, "base", 0);
        var cacheKey = GetCacheKey(themeId, TerrainAssetType.Base, "base", selectedVariant);
        
        return _imageCache.GetValueOrDefault(cacheKey);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetTerrainOverlayImage(string themeId, string terrainType, int? variant = null)
    {
        await EnsureInitialized();
        
        var variants = GetAvailableVariants(themeId, TerrainAssetType.Overlay, terrainType);
        if (variants.Count == 0) return null;
        
        var selectedVariant = variant ?? SelectRandomVariant(variants, themeId, terrainType, 0);
        var cacheKey = GetCacheKey(themeId, TerrainAssetType.Overlay, terrainType, selectedVariant);
        
        return _imageCache.GetValueOrDefault(cacheKey);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetEdgeImage(string themeId, HexDirection direction, TerrainAssetType edgeType, HexCoordinates coordinates)
    {
        await EnsureInitialized();
        
        if (edgeType is not (TerrainAssetType.EdgeTop or TerrainAssetType.EdgeBottom))
            return null;
        
        var directionName = ((int)direction).ToString();
        var variants = GetAvailableVariants(themeId, edgeType, directionName);
        if (variants.Count == 0) return null;
        
        // Use hex coordinates for deterministic variant selection
        var selectedVariant = SelectRandomVariant(variants, themeId, directionName, coordinates.Q + coordinates.R * 31);
        var cacheKey = GetCacheKey(themeId, edgeType, directionName, selectedVariant);
        
        return _imageCache.GetValueOrDefault(cacheKey);
    }

    /// <inheritdoc />
    public IReadOnlyList<int> GetAvailableVariants(string themeId, TerrainAssetType assetType, string assetName)
    {
        var variantKey = GetVariantKey(themeId, assetType, assetName);
        return _variantCache.TryGetValue(variantKey, out var variants) 
            ? variants 
            : Array.Empty<int>();
    }

    /// <inheritdoc />
    public async Task<TerrainThemeManifest?> LoadTerrainFromMmtxStreamAsync(Stream mmtxStream)
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

            TerrainThemeManifest manifest;
            await using (var manifestStream = await manifestEntry.OpenAsync())
            using (var reader = new StreamReader(manifestStream))
            {
                var jsonContent = await reader.ReadToEndAsync();
                manifest = JsonSerializer.Deserialize<TerrainThemeManifest>(jsonContent, _jsonOptions)
                    ?? throw new InvalidOperationException("Failed to deserialize manifest.json");
            }
            
            if (string.IsNullOrEmpty(manifest.ThemeId))
            {
                _logger.LogWarning("MMTX package manifest missing themeId");
                return null;
            }

            // Extract and cache all images
            await ExtractImagesAsync(archive, manifest.ThemeId);
            
            // Cache the manifest
            _themeManifests.TryAdd(manifest.ThemeId, manifest);
            
            _logger.LogInformation("Loaded terrain theme '{ThemeId}' version {Version}", 
                manifest.ThemeId, manifest.Version);
            
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
        _themeManifests.Clear();
        _imageCache.Clear();
        _variantCache.Clear();
        _isInitialized = false;
    }

    private async Task EnsureInitialized()
    {
        if (_isInitialized) return;

        lock (_initializationLock)
        {
            if (_isInitialized) return;
            LoadTerrainFromStreamProviders().GetAwaiter().GetResult();
            _isInitialized = true;
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

    private async Task ExtractImagesAsync(ZipArchive archive, string themeId)
    {
        // Extract base terrain images
        await ExtractImagesFromDirectoryAsync(archive, themeId, "", TerrainAssetType.Base);
        
        // Extract terrain overlay images
        await ExtractImagesFromDirectoryAsync(archive, themeId, "terrains/", TerrainAssetType.Overlay);
        
        // Extract edge images
        await ExtractEdgeImagesAsync(archive, themeId);
    }

    private async Task ExtractImagesFromDirectoryAsync(
        ZipArchive archive, 
        string themeId, 
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
            
            var cacheKey = GetCacheKey(themeId, assetType, assetName, variant);
            _imageCache.TryAdd(cacheKey, memoryStream.ToArray());
            
            // Track variants
            var variantKey = GetVariantKey(themeId, assetType, assetName);
            _variantCache.AddOrUpdate(
                variantKey,
                _ => ImmutableSortedSet.Create(variant),
                (_, set) => set.Add(variant));
        }
    }

    private async Task ExtractEdgeImagesAsync(ZipArchive archive, string themeId)
    {
        var edgesDirectory = "edges/";
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
            
            var cacheKey = GetCacheKey(themeId, assetType, direction, variant);
            _imageCache.TryAdd(cacheKey, memoryStream.ToArray());
            
            // Track variants
            var variantKey = GetVariantKey(themeId, assetType, direction);
            _variantCache.AddOrUpdate(
                variantKey,
                _ => ImmutableSortedSet.Create(variant),
                (_, set) => set.Add(variant));
        }
    }

    /// <summary>
    /// Parses an asset file name into asset name and variant number
    /// Examples: "base-1" -> ("base", 0), "lightwoods-2" -> ("lightwoods", 1)
    /// </summary>
    private static (string assetName, int variant) ParseAssetFileName(string fileName)
    {
        var lastDashIndex = fileName.LastIndexOf('-');
        if (lastDashIndex < 0)
            return (fileName.ToLowerInvariant(), 0);
        
        var namePart = fileName[..lastDashIndex];
        var variantPart = fileName[(lastDashIndex + 1)..];
        
        if (int.TryParse(variantPart, out var variantNum))
            return (namePart.ToLowerInvariant(), variantNum - 1); // Convert to 0-indexed
        
        return (fileName.ToLowerInvariant(), 0);
    }

    /// <summary>
    /// Parses an edge file name into edge type, direction, and variant
    /// Examples: "top-0-1" -> ("top", "0", 0), "bottom-5-3" -> ("bottom", "5", 2)
    /// </summary>
    private static (string? edgeType, string direction, int variant) ParseEdgeFileName(string fileName)
    {
        var parts = fileName.Split('-');
        
        if (parts.Length < 3)
            return (null, "0", 0);
        
        var edgeType = parts[0].ToLowerInvariant();
        if (edgeType is not ("top" or "bottom"))
            return (null, "0", 0);
        
        var direction = parts[1];
        if (!int.TryParse(direction, out _))
            return (null, "0", 0);
        
        var variant = 0;
        if (parts.Length >= 3 && int.TryParse(parts[2], out var variantNum))
            variant = variantNum - 1; // Convert to 0-indexed
        
        return (edgeType, direction, variant);
    }

    private static string GetCacheKey(string themeId, TerrainAssetType assetType, string assetName, int variant)
    {
        return $"{themeId}/{assetType}/{assetName}/{variant}";
    }

    private static string GetVariantKey(string themeId, TerrainAssetType assetType, string assetName)
    {
        return $"{themeId}/{assetType}/{assetName}";
    }

    /// <summary>
    /// Selects a variant deterministically based on a seed value
    /// Uses hash-based selection for consistent results across sessions
    /// </summary>
    private static int SelectRandomVariant(IReadOnlyList<int> variants, string themeId, string assetName, int seed)
    {
        if (variants.Count == 0) return 0;
        if (variants.Count == 1) return variants[0];
        
        // Combine theme, asset name, and seed for deterministic selection
        var combined = $"{themeId}-{assetName}-{seed}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        var hashValue = BitConverter.ToUInt32(hash, 0);
        
        var index = (int)(hashValue % (uint)variants.Count);
        return variants[index];
    }
}
