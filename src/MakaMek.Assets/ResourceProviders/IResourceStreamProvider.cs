namespace Sanet.MakaMek.Assets.ResourceProviders;

/// <summary>
/// Interface for providing resource streams from various sources (assemblies, filesystem, network, etc.)
/// </summary>
public interface IResourceStreamProvider
{
    /// <summary>
    /// Stable identifier of this provider (matches the configured provider id, e.g. "bucket",
    /// "local"). Used to attribute cached resources to the provider that loaded them.
    /// </summary>
    string Id { get; }

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
