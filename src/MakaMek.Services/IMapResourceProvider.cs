using Sanet.MakaMek.Map.Data;

namespace Sanet.MakaMek.Services;

/// <summary>
/// Interface for providing pre-existing map data from various sources
/// </summary>
public interface IMapResourceProvider
{
    /// <summary>
    /// Gets all available maps with their names and hex data
    /// </summary>
    Task<IReadOnlyList<(string Name, IList<HexData> HexData)>> GetAvailableMapsAsync();
}
