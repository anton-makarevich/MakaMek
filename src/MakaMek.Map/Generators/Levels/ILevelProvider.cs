using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Generators.Levels;

/// <summary>
/// Provides elevation levels for individual hex coordinates.
/// </summary>
public interface ILevelProvider
{
    /// <summary>
    /// Returns the elevation level for the hex at the specified coordinates.
    /// </summary>
    int GetLevel(HexCoordinates coordinates);
}

