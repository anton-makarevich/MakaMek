namespace Sanet.MakaMek.Core.Services;

/// <summary>
/// Interface for providing MMUX unit streams from various sources (assemblies, filesystem, network, etc.)
/// </summary>
public interface IUnitStreamProvider
{
    /// <summary>
    /// Gets all available unit identifiers from this provider
    /// </summary>
    /// <returns>Collection of unit identifiers</returns>
    IEnumerable<string> GetAvailableUnitIds();

    /// <summary>
    /// Gets a MMUX stream for the specified unit identifier
    /// </summary>
    /// <param name="unitId">The unit identifier</param>
    /// <returns>Stream containing MMUX package data, or null if not found</returns>
    Task<Stream?> GetUnitStream(string unitId);
}
