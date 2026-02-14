using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Generators;

/// <summary>
/// Interface for terrain generation strategies
/// </summary>
public interface ITerrainGenerator
{
    /// <summary>
    /// Generates terrain for a hex at the given coordinates
    /// </summary>
    Hex Generate(HexCoordinates coordinates);
}
