using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Services;

/// <summary>
/// Units loader that retrieves units from the UnitCachingService
/// This replaces the embedded resources loader for MMUX package support
/// </summary>
public class MmuxUnitsLoader : IUnitsLoader
{
    private readonly UnitCachingService _unitCachingService;

    public MmuxUnitsLoader(UnitCachingService unitCachingService)
    {
        _unitCachingService = unitCachingService;
    }

    /// <summary>
    /// Loads all available units from the caching service
    /// </summary>
    /// <returns>List of all available unit data</returns>
    public Task<List<UnitData>> LoadUnits()
    {
        var units = _unitCachingService.GetAllUnits().ToList();
        return Task.FromResult(units);
    }
}
