using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Models.Logger;
using Sanet.MakaMek.Assets.Models.Terrains;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Assets.ResourceProviders;

namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Service for caching and retrieving terrain assets from MMTX packages.
/// Owns the cache state and its lifetime; terrain → image mapping is delegated to
/// <see cref="ITerrainImageResolver"/>, cache bookkeeping to <see cref="TerrainAssetCache"/>
/// and package loading to <see cref="TerrainPackageLoader"/>.
/// </summary>
public class TerrainCachingService : PackageCacheCore<TerrainCachingService.TerrainCacheState>, ITerrainAssetService
{
    private readonly ILogger<TerrainCachingService> _logger;
    private readonly TerrainAssetCache _assetCache;
    private readonly ITerrainImageResolver _imageResolver;
    private readonly TerrainPackageLoader _packageLoader;

    /// <summary>
    /// Snapshot of all cached terrain data.
    /// </summary>
    public sealed class TerrainCacheState : PackageCacheState
    {
        /// <summary>
        /// Synchronizes package-merge operations so a duplicate biome is replaced by one complete
        /// package rather than an interleaved mixture from concurrent callers on this same state.
        /// </summary>
        public object SyncRoot { get; } = new();

        public ConcurrentDictionary<string, BiomeManifest> BiomeManifests { get; } = new();
        public ConcurrentDictionary<string, byte[]> ImageCache { get; } = new();
        public ConcurrentDictionary<string, ImmutableSortedSet<int>> VariantCache { get; } = new();
    }

    /// <summary>
    /// Initializes a new instance of TerrainCachingService with its collaborators supplied by
    /// the composition root.
    /// </summary>
    /// <param name="streamProviders">Collection of stream providers to load terrain from</param>
    /// <param name="loggerFactory">Logger factory for logging</param>
    /// <param name="lazyProviders">Optional async factory for lazily resolving providers on first access</param>
    /// <param name="assetCache">Cache bookkeeping used for image and variant lookups</param>
    /// <param name="imageResolver">Maps terrain requests to package asset coordinates</param>
    /// <param name="packageLoader">Loads MMTX packages and merges them into the cache state</param>
    public TerrainCachingService(
        IEnumerable<IResourceStreamProvider> streamProviders,
        ILoggerFactory loggerFactory,
        Func<Task<IReadOnlyList<IResourceStreamProvider>>>? lazyProviders,
        TerrainAssetCache assetCache,
        ITerrainImageResolver imageResolver,
        TerrainPackageLoader packageLoader)
        : base(streamProviders)
    {
        _logger = loggerFactory.CreateLogger<TerrainCachingService>();
        _assetCache = assetCache;
        _imageResolver = imageResolver;
        _packageLoader = packageLoader;
        if (lazyProviders is not null)
            SetLazyProviders(lazyProviders);
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
        return _assetCache.GetImage(state, biomeId, _imageResolver.ResolveBase(variant));
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetTerrainOverlayImage(string biomeId, string terrainType, int? variant = null)
    {
        var state = await EnsureInitialized();
        return _assetCache.GetImage(state, biomeId, _imageResolver.ResolveOverlay(terrainType, variant));
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetEdgeImage(string biomeId, HexDirection direction, TerrainAssetType edgeType, HexCoordinates coordinates)
    {
        var state = await EnsureInitialized();

        var request = _imageResolver.ResolveEdge(direction, edgeType, coordinates);
        return request is null ? null : _assetCache.GetImage(state, biomeId, request.Value);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetWaterTextureImage(string biomeId, CanonicalBitmaskResult canonicalBitmask)
    {
        var state = await EnsureInitialized();
        return _assetCache.GetImage(state, biomeId, _imageResolver.ResolveWaterTexture(canonicalBitmask));
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetRoadTextureImage(string biomeId, CanonicalBitmaskResult canonicalBitmask)
    {
        var state = await EnsureInitialized();
        return _assetCache.GetImage(state, biomeId, _imageResolver.ResolveRoadTexture(canonicalBitmask));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<int>> GetAvailableVariants(string biomeId, TerrainAssetType assetType, string assetName)
    {
        var state = await EnsureInitialized();
        return _assetCache.GetVariants(state, biomeId, assetType, assetName);
    }

    /// <inheritdoc />
    public async Task<BiomeManifest?> LoadTerrainFromMmtxStream(Stream mmtxStream)
    {
        try
        {
            // Resolve the state after parsing so a cache cleared while the reader was suspended
            // is not populated with the discarded snapshot.
            var (manifest, replacedExistingBiome) = await _packageLoader.Load(
                mmtxStream, () => CurrentState, providerId: null, onDuplicateAsset: LogDuplicateAsset);
            LogDuplicateBiomeIfReplaced(replacedExistingBiome, manifest.Id);
            return manifest;
        }
        catch (Exception ex)
        {
            _logger.LogErrorLoadingMmtxPackage(ex);
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
        // Provider loads stay bound to the explicit staging state handed to this call.
        var (manifest, replacedExistingBiome) = await _packageLoader.Load(
            stream, () => state, provider.Id, LogDuplicateAsset, cancellationToken);
        LogDuplicateBiomeIfReplaced(replacedExistingBiome, manifest.Id);
        _logger.LogLoadedTerrainBiome(manifest.Id, manifest.Version);
    }

    private void LogDuplicateBiomeIfReplaced(bool replacedExistingBiome, string biomeId)
    {
        if (replacedExistingBiome)
        {
            _logger.LogDuplicateTerrainBiome(biomeId);
        }
    }

    private void LogDuplicateAsset(string cacheKey) =>
        _logger.LogDuplicateResourceFound(ResourceKind, cacheKey);
}
