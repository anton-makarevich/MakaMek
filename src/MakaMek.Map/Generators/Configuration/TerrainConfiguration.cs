using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Generators.Configuration;

/// <summary>
/// Holds the configuration for a terrain overlay: the terrain type and its coverage fraction.
/// </summary>
/// <param name="Terrain">The terrain instance to place in matching hexes.</param>
/// <param name="Coverage">Fraction of map hexes to cover (0.0–1.0).</param>
public record TerrainConfiguration(Terrain Terrain, double Coverage);
