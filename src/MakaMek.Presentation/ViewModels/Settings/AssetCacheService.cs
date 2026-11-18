using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Presentation.ViewModels.Settings;

/// <summary>
/// Clears every download cache the settings screen exposes. Kept separate so the view model does not
/// have to know which caching services exist.
/// </summary>
public sealed class AssetCacheService
{
    private readonly IFileCachingService _fileCachingService;
    private readonly IUnitCachingService _unitCachingService;
    private readonly ITerrainAssetService _terrainAssetService;

    public AssetCacheService(
        IFileCachingService fileCachingService,
        IUnitCachingService unitCachingService,
        ITerrainAssetService terrainAssetService)
    {
        _fileCachingService = fileCachingService;
        _unitCachingService = unitCachingService;
        _terrainAssetService = terrainAssetService;
    }

    public async Task ClearAllAsync()
    {
        await _fileCachingService.ClearCache();
        await _unitCachingService.ClearCache();
        await _terrainAssetService.ClearCache();
    }
}
