using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Assets.ResourceProviders;

namespace Sanet.MakaMek.Assets.Services;

public interface IUnitCachingService : IProgressReporting
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
    Task ClearCache();

    /// <summary>
    /// Replaces the set of providers the cache loads from and forces a lazy re-initialization.
    /// Existing in-memory caches are cleared so the next access loads from the new provider set.
    /// </summary>
    /// <param name="providers">The new ordered provider list</param>
    Task SetProviders(IEnumerable<IResourceStreamProvider> providers);

    /// <summary>
    /// Clears all cached data and re-runs initialization from the current provider set.
    /// This is used by the Settings reload flow to refresh assets after providers change.
    /// </summary>
    Task ReloadProviders();
}
