namespace Sanet.MakaMek.Core.Services;

/// <summary>
/// Interface for providing resource streams from various sources (assemblies, filesystem, network, etc.)
/// </summary>
public interface IResourceStreamProvider
{
    /// <summary>
    /// Gets all available resource identifiers from this provider
    /// </summary>
    /// <returns>Collection of resource identifiers</returns>
    Task<IEnumerable<string>> GetAvailableResourceIds();

    /// <summary>
    /// Gets a stream for the specified resource identifier
    /// </summary>
    /// <param name="resourceId">The resource identifier</param>
    /// <returns>Stream containing resource package data, or null if not found</returns>
    Task<Stream?> GetResourceStream(string resourceId);
}
