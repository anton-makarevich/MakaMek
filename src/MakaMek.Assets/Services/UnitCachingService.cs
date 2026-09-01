using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Assets.ResourceProviders;
using Sanet.MakaMek.Assets.Services.PackageReaders;

namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Service for caching unit data and images loaded from various sources including MMUX packages
/// </summary>
public class UnitCachingService : PackageCacheCore<UnitCachingService.UnitCacheState>, IUnitCachingService
{
    private readonly ILogger<UnitCachingService> _logger;
    private readonly MmuxUnitPackageReader _packageReader = new();

    /// <summary>
    /// Snapshot of all cached unit data.
    /// </summary>
    public sealed class UnitCacheState : PackageCacheState
    {
        public readonly ConcurrentDictionary<string, UnitData> UnitDataCache = new();
        public readonly ConcurrentDictionary<string, byte[]> ImageCache = new();
    }

    /// <summary>
    /// Initializes a new instance of UnitCachingService
    /// </summary>
    /// <param name="streamProviders">Collection of stream providers to load units from</param>
    /// <param name="loggerFactory">Logger factory for logging</param>
    public UnitCachingService(IEnumerable<IResourceStreamProvider> streamProviders, ILoggerFactory loggerFactory)
        : base(streamProviders)
    {
        _logger = loggerFactory.CreateLogger<UnitCachingService>();
    }

    /// <inheritdoc />
    protected override string ResourceKind => "unit";

    /// <inheritdoc />
    protected override ILogger Logger => _logger;

    /// <summary>
    /// Gets unit data by model name
    /// </summary>
    /// <param name="model">The unit model identifier</param>
    /// <returns>Unit data if found, null otherwise</returns>
    public async Task<UnitData?> GetUnitData(string model)
    {
        var state = await EnsureInitialized();
        return state.UnitDataCache.TryGetValue(model, out var unitData) ? unitData : null;
    }

    /// <summary>
    /// Gets unit image by model name
    /// </summary>
    /// <param name="model">The unit model identifier</param>
    /// <returns>Image bytes if found, null otherwise</returns>
    public async Task<byte[]?> GetUnitImage(string model)
    {
        var state = await EnsureInitialized();
        return state.ImageCache.GetValueOrDefault(model);
    }

    /// <summary>
    /// Gets all available unit models
    /// </summary>
    /// <returns>Collection of unit model identifiers</returns>
    public async Task<IEnumerable<string>> GetAvailableModels()
    {
        var state = await EnsureInitialized();
        return state.UnitDataCache.Keys;
    }

    /// <summary>
    /// Gets all cached unit data
    /// </summary>
    /// <returns>Collection of all unit data</returns>
    public async Task<IEnumerable<UnitData>> GetAllUnits()
    {
        var state = await EnsureInitialized();
        return state.UnitDataCache.Values;
    }

    /// <inheritdoc />
    protected override async Task LoadResource(
        IResourceStreamProvider provider,
        string resourceId,
        Stream stream,
        UnitCacheState state,
        CancellationToken cancellationToken = default)
    {
        var package = await _packageReader.Read(stream, cancellationToken);
        AddPackageToCache(package, state);
    }

    /// <summary>
    /// Commits a parsed unit package (model data + image) to the cache as one entry, using the
    /// provided provider-list precedence: a duplicate model from a provider lower in the list
    /// replaces the complete earlier package. Both the data and the image are committed together
    /// so a duplicate is never an interleaved mixture of different packages.
    /// </summary>
    private void AddPackageToCache(UnitPackage package, UnitCacheState state)
    {
        var model = package.Data.Model;
        TryCache(state.UnitDataCache, model, package.Data);
        TryCache(state.ImageCache, model, package.Image);
    }
}