using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Services;

public interface IUnitCachingService
{
    /// <summary>
    /// Gets unit data by model name
    /// </summary>
    /// <param name="model">The unit model identifier</param>
    /// <returns>Unit data if found, null otherwise</returns>
    Task<UnitData?> GetUnitData(string model);

    /// <summary>
    /// Gets unit image by model name
    /// </summary>
    /// <param name="model">The unit model identifier</param>
    /// <returns>Image bytes if found, null otherwise</returns>
    Task<byte[]?> GetUnitImage(string model);

    /// <summary>
    /// Gets all available unit models
    /// </summary>
    /// <returns>Collection of unit model identifiers</returns>
    Task<IEnumerable<string>> GetAvailableModels();

    /// <summary>
    /// Gets all cached unit data
    /// </summary>
    /// <returns>Collection of all unit data</returns>
    Task<IEnumerable<UnitData>> GetAllUnits();

    /// <summary>
    /// Clears all cached data (useful for testing or reloading)
    /// </summary>
    void ClearCache();
}