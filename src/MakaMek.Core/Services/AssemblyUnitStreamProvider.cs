using System.Reflection;

namespace Sanet.MakaMek.Core.Services;

/// <summary>
/// Unit stream provider that loads MMUX packages from embedded assembly resources
/// </summary>
public class AssemblyUnitStreamProvider : IUnitStreamProvider
{
    private readonly Assembly? _hostAssembly;
    private readonly Lazy<Dictionary<string, string>> _unitIdToResourceMap;

    /// <summary>
    /// Initializes a new instance of AssemblyUnitStreamProvider
    /// </summary>
    /// <param name="hostAssembly">Assembly to scan for MMUX resources. If null, use entry assembly.</param>
    public AssemblyUnitStreamProvider(Assembly? hostAssembly = null)
    {
        _hostAssembly = hostAssembly;
        _unitIdToResourceMap = new Lazy<Dictionary<string, string>>(BuildUnitIdToResourceMap);
    }

    /// <summary>
    /// Gets all available unit identifiers from embedded assembly resources
    /// </summary>
    /// <returns>Collection of unit identifiers</returns>
    public IEnumerable<string> GetAvailableUnitIds()
    {
        return _unitIdToResourceMap.Value.Keys;
    }

    /// <summary>
    /// Gets a MMUX stream for the specified unit identifier
    /// </summary>
    /// <param name="unitId">The unit identifier</param>
    /// <returns>Stream containing MMUX package data, or null if not found</returns>
    public Task<Stream?> GetUnitStream(string unitId)
    {
        if (string.IsNullOrEmpty(unitId) || !_unitIdToResourceMap.Value.TryGetValue(unitId, out var resourceName))
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
            var stream = assembly.GetManifestResourceStream(resourceName);
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
    private Dictionary<string, string> BuildUnitIdToResourceMap()
    {
        var map = new Dictionary<string, string>();
        var assembly = GetTargetAssembly();
        
        if (assembly == null)
        {
            return map;
        }

        var resources = assembly.GetManifestResourceNames();
        
        foreach (var resourceName in resources)
        {
            if (!resourceName.EndsWith(".mmux", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Extract unit ID from resource name
            // Resource names typically follow pattern: "Namespace.Path.UnitModel.mmux"
            var unitId = ExtractUnitIdFromResourceName(resourceName);
            if (!string.IsNullOrEmpty(unitId))
            {
                map[unitId] = resourceName;
            }
        }

        return map;
    }

    /// <summary>
    /// Extracts unit ID from a resource name
    /// </summary>
    /// <param name="resourceName">Full resource name</param>
    /// <returns>Unit ID extracted from a resource name</returns>
    private static string ExtractUnitIdFromResourceName(string resourceName)
    {
        // Extract filename without extension from the resource name
        // Example: "Sanet.MakaMek.Avalonia.Resources.Units.Mechs.LCT-1V.mmux" -> "LCT-1V"
        var lastDotIndex = resourceName.LastIndexOf('.');
        if (lastDotIndex <= 0)
        {
            return string.Empty;
        }

        var secondLastDotIndex = resourceName.LastIndexOf('.', lastDotIndex - 1);
        if (secondLastDotIndex < 0)
        {
            return string.Empty;
        }

        return resourceName.Substring(secondLastDotIndex + 1, lastDotIndex - secondLastDotIndex - 1);
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
