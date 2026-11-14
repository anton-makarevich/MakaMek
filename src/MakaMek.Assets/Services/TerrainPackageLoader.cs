using Sanet.MakaMek.Assets.Models.Terrains;
using Sanet.MakaMek.Assets.Services.PackageReaders;

namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Loads an MMTX package from a stream and populates a terrain cache state through
/// <see cref="TerrainAssetCache"/>. Owns the package reader and the cache-population path so
/// <see cref="TerrainCachingService"/> only coordinates providers, lifecycle and lookups.
/// </summary>
public sealed class TerrainPackageLoader
{
    private readonly MmtxTerrainPackageReader _packageReader;
    private readonly TerrainAssetCache _assetCache;

    /// <summary>
    /// Initializes a new instance of TerrainPackageLoader
    /// </summary>
    /// <param name="packageReader">Format-specific reader for MMTX packages</param>
    /// <param name="assetCache">Cache bookkeeping used to merge the parsed package</param>
    public TerrainPackageLoader(MmtxTerrainPackageReader packageReader, TerrainAssetCache assetCache)
    {
        _packageReader = packageReader;
        _assetCache = assetCache;
    }

    /// <summary>
    /// Parses an MMTX package stream and merges it into the given cache state, applying the
    /// shared duplicate policy (a package from a provider lower in the list replaces an earlier one).
    /// </summary>
    /// <param name="packageStream">Stream containing the MMTX package data</param>
    /// <param name="state">Cache state to merge into</param>
    /// <param name="providerId">Provider the package came from, or <c>null</c> for direct stream loads</param>
    /// <param name="onDuplicateAsset">
    /// Invoked with the cache key when an image overwrites an already cached one
    /// </param>
    /// <param name="cancellationToken">Optional cancellation token propagated to the package reader</param>
    /// <returns>The loaded biome manifest and whether an already cached biome was replaced</returns>
    public async Task<(BiomeManifest Manifest, bool ReplacedExistingBiome)> Load(
        Stream packageStream,
        TerrainCachingService.TerrainCacheState state,
        string? providerId,
        Action<string>? onDuplicateAsset = null,
        CancellationToken cancellationToken = default)
    {
        var package = await _packageReader.Read(packageStream, cancellationToken);
        var replacedExistingBiome = _assetCache.AddPackage(state, package, providerId, onDuplicateAsset);
        return (package.Manifest, replacedExistingBiome);
    }
}
