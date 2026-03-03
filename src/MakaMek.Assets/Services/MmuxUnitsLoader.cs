using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Unit loader that retrieves units from the UnitCachingService
/// This replaces the embedded resources loader for MMUX package support
/// </summary>
public class MmuxUnitsLoader : Core.Services.IUnitsLoader
{
    private readonly Core.Services.IUnitCachingService _unitCachingService;

    public MmuxUnitsLoader(Core.Services.IUnitCachingService unitCachingService)
    {
        _unitCachingService = unitCachingService;
    }

    /// <summary>
    /// Loads all available units from the caching service
    /// </summary>
    /// <returns>List of all available unit data</returns>
    public async Task<List<UnitData>> LoadUnits()
    {
        var units = await _unitCachingService.GetAllUnits();
        return units.ToList();
    }
}
