using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Models.Terrains;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Assets.ResourceProviders;
using Sanet.MakaMek.Assets.Services.PackageReaders;

namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Service for caching and retrieving terrain assets from MMTX packages.
/// Owns the cache state and its lifetime; terrain → image mapping is delegated to
/// <see cref="ITerrainImageResolver"/>, cache bookkeeping to <see cref="TerrainAssetCache"/>
/// and package loading to <see cref="TerrainPackageLoader"/>.
/// </summary>
public class TerrainCachingService : PackageCacheCore<TerrainCacheState>, ITerrainAssetService
{
    private readonly ILogger<TerrainCachingService> _logger;
    private readonly ITerrainImageResolver _imageResolver;
    private readonly TerrainPackageLoader _packageLoader;

    /// <summary>
    /// Initializes a new instance of TerrainCachingService using the default stateless collaborators.
    /// </summary>
    /// <param name="streamProviders">Collection of stream providers to load terrain from</param>
    /// <param name="loggerFactory">Logger factory for logging</param>
    /// <param name="lazyProviders">Optional async factory for lazily resolving providers on first access</param>
    public TerrainCachingService(
        IEnumerable<IResourceStreamProvider> streamProviders,
        ILoggerFactory loggerFactory,
        Func<Task<IReadOnlyList<IResourceStreamProvider>>>? lazyProviders = null)
        : this(streamProviders, loggerFactory, lazyProviders, Collaborators.CreateDefault())
    {
    }

    /// <summary>
    /// Initializes a new instance of TerrainCachingService with explicit collaborators.
    /// The composition root uses this overload so the resolver, cache and loader are injected.
    /// </summary>
    /// <param name="streamProviders">Collection of stream providers to load terrain from</param>
    /// <param name="loggerFactory">Logger factory for logging</param>
    /// <param name="lazyProviders">Optional async factory for lazily resolving providers on first access</param>
    /// <param name="imageResolver">Maps terrain requests to package asset coordinates</param>
    /// <param name="packageLoader">Loads MMTX packages and merges them into the cache state</param>
    public TerrainCachingService(
        IEnumerable<IResourceStreamProvider> streamProviders,
        ILoggerFactory loggerFactory,
        Func<Task<IReadOnlyList<IResourceStreamProvider>>>? lazyProviders,
        ITerrainImageResolver imageResolver,
        TerrainPackageLoader packageLoader)
        : this(streamProviders, loggerFactory, lazyProviders,
            new Collaborators(imageResolver, packageLoader))
    {
    }

    private TerrainCachingService(
        IEnumerable<IResourceStreamProvider> streamProviders,
        ILoggerFactory loggerFactory,
        Func<Task<IReadOnlyList<IResourceStreamProvider>>>? lazyProviders,
        Collaborators collaborators)
        : base(streamProviders)
    {
        _logger = loggerFactory.CreateLogger<TerrainCachingService>();
        _imageResolver = collaborators.ImageResolver;
        _packageLoader = collaborators.PackageLoader;
        if (lazyProviders is not null)
            SetLazyProviders(lazyProviders);
    }

    /// <summary>
    /// Default collaborators for hosts that build the service directly (tests, headless harness).
    /// </summary>
    private sealed record Collaborators(
        ITerrainImageResolver ImageResolver,
        TerrainPackageLoader PackageLoader)
    {
        public static Collaborators CreateDefault()
        {
            var assetCache = new TerrainAssetCache();
            return new Collaborators(
                new TerrainImageResolver(),
                new TerrainPackageLoader(new MmtxTerrainPackageReader(), assetCache));
        }
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
        return TerrainAssetCache.GetImage(state, biomeId, _imageResolver.ResolveBase(variant));
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetTerrainOverlayImage(string biomeId, string terrainType, int? variant = null)
    {
        var state = await EnsureInitialized();
        return TerrainAssetCache.GetImage(state, biomeId, _imageResolver.ResolveOverlay(terrainType, variant));
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetEdgeImage(string biomeId, HexDirection direction, TerrainAssetType edgeType, HexCoordinates coordinates)
    {
        var state = await EnsureInitialized();

        var request = _imageResolver.ResolveEdge(direction, edgeType, coordinates);
        return request is null ? null : TerrainAssetCache.GetImage(state, biomeId, request.Value);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetWaterTextureImage(string biomeId, CanonicalBitmaskResult canonicalBitmask)
    {
        var state = await EnsureInitialized();
        return TerrainAssetCache.GetImage(state, biomeId, _imageResolver.ResolveWaterTexture(canonicalBitmask));
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetRoadTextureImage(string biomeId, CanonicalBitmaskResult canonicalBitmask)
    {
        var state = await EnsureInitialized();
        return TerrainAssetCache.GetImage(state, biomeId, _imageResolver.ResolveRoadTexture(canonicalBitmask));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<int>> GetAvailableVariants(string biomeId, TerrainAssetType assetType, string assetName)
    {
        var state = await EnsureInitialized();
        return TerrainAssetCache.GetVariants(state, biomeId, assetType, assetName);
    }

    /// <inheritdoc />
    public async Task<BiomeManifest?> LoadTerrainFromMmtxStream(Stream mmtxStream)
    {
        try
        {
            var (manifest, replacedExistingBiome) = await _packageLoader.Load(
                mmtxStream, CurrentState, providerId: null, onDuplicateAsset: LogDuplicateAsset);
            LogDuplicateBiomeIfReplaced(replacedExistingBiome, manifest.Id);
            return manifest;
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
        var (manifest, replacedExistingBiome) = await _packageLoader.Load(
            stream, state, provider.Id, LogDuplicateAsset, cancellationToken);
        LogDuplicateBiomeIfReplaced(replacedExistingBiome, manifest.Id);
        _logger.LogInformation("Loaded terrain biome '{BiomeId}' version {Version}",
            manifest.Id, manifest.Version);
    }

    private void LogDuplicateBiomeIfReplaced(bool replacedExistingBiome, string biomeId)
    {
        if (replacedExistingBiome)
        {
            _logger.LogWarning("Duplicate terrain biome '{BiomeId}' found; replacing previously loaded assets",
                biomeId);
        }
    }

    private void LogDuplicateAsset(string cacheKey) =>
        _logger.LogWarning("Duplicate {ResourceKind} '{Key}' found; overwriting with the value from a lower provider",
            ResourceKind, cacheKey);
}
