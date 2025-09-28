using System.Reflection;

namespace Sanet.MakaMek.Core.Services;

/// <summary>
/// Unit stream provider that loads unit packages from embedded assembly resources
/// </summary>
public class AssemblyResourceStreamProvider : IResourceStreamProvider
{
    private readonly Assembly? _hostAssembly;
    private readonly string _resourceType;
    private readonly Lazy<List<string>> _unitIdToResourceMap;

    /// <summary>
    /// Initializes a new instance of AssemblyUnitStreamProvider
    /// </summary>
    /// <param name="resourceType">The type of resources to load (e.g., "mmux", "json", "xml")</param>
    /// <param name="hostAssembly">Assembly to scan for resources. If null, use entry assembly.</param>
    public AssemblyResourceStreamProvider(string resourceType, Assembly? hostAssembly = null)
    {
        _resourceType = resourceType ?? throw new ArgumentNullException(nameof(resourceType));
        _hostAssembly = hostAssembly;
        _unitIdToResourceMap = new Lazy<List<string>>(BuildUnitIdToResourceList);
    }

    /// <summary>
    /// Gets all available unit identifiers from embedded assembly resources
    /// </summary>
    /// <returns>Collection of unit identifiers</returns>
    public Task<IEnumerable<string>> GetAvailableResourceIds()
    {
        return Task.FromResult<IEnumerable<string>>(_unitIdToResourceMap.Value);
    }

    /// <summary>
    /// Gets a stream for the specified unit identifier
    /// </summary>
    /// <param name="resourceId">The unit identifier</param>
    /// <returns>Stream containing unit package data, or null if not found</returns>
    public Task<Stream?> GetResourceStream(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId) || !_unitIdToResourceMap.Value.Contains(resourceId))
        {
            return Task.FromResult<Stream?>(null);
        }

        var assembly = GetTargetAssembly();
        if (assembly == null)
        {
            return Task.FromResult<Stream?>(null);
        }

        try
        {
            var stream = assembly.GetManifestResourceStream(resourceId);
            return Task.FromResult(stream);
        }
        catch
        {
            // TODO: log error but return null to gracefully handle resources
            return Task.FromResult<Stream?>(null);
        }
    }

    /// <summary>
    /// Builds a mapping from unit IDs to resource names by scanning assembly resources
    /// </summary>
    /// <returns>Dictionary mapping unit IDs to resource names</returns>
    private List<string> BuildUnitIdToResourceList()
    {
        var list = new List<string>();
        var assembly = GetTargetAssembly();

        if (assembly == null)
        {
            return list;
        }

        var resources = assembly.GetManifestResourceNames();

        foreach (var resourceName in resources)
        {
            if (!resourceName.EndsWith($".{_resourceType}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            
            list.Add(resourceName);
        }

        return list;
    }

    /// <summary>
    /// Gets the target assembly to scan for resources
    /// </summary>
    /// <returns>Assembly to scan, or null if not available</returns>
    private Assembly? GetTargetAssembly()
    {
        return _hostAssembly ?? Assembly.GetEntryAssembly();
    }
}
