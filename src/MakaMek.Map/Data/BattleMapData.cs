namespace Sanet.MakaMek.Map.Data;

/// <summary>
/// Data transfer object for serializing/deserializing a battle map,
/// including map-level properties like biome and the collection of hex data.
/// </summary>
public record BattleMapData
{
    /// <summary>
    /// The biome identifier for the entire map (e.g., "makamek.biomes.grasslands")
    /// </summary>
    public string Biome { get; init; } = "makamek.biomes.grasslands";

    /// <summary>
    /// The hex data for all hexes on the map
    /// </summary>
    public required List<HexData> HexData { get; init; }
}

