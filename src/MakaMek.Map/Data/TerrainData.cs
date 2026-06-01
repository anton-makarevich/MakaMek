using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Data;

/// <summary>
/// Data transfer object for terrain serialization.
/// Preserves terrain-specific properties like water depth.
/// </summary>
public record TerrainData
{
    /// <summary>
    /// The terrain type identifier
    /// </summary>
    public required MakaMekTerrains Type { get; init; }

    /// <summary>
    /// The height/depth of the terrain.
    /// For water terrains: 0 = shallow, -1 = standard, -2+ = deep.
    /// For bridge terrains: positive integer for bridge surface elevation.
    /// Null for terrains where height is not variable (e.g., Clear, Woods).
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    /// Construction factor for structural terrains (e.g., bridges).
    /// Represents the maximum tonnage the structure can support.
    /// </summary>
    public int? ConstructionFactor { get; init; }
}
