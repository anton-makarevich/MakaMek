namespace Sanet.MakaMek.Map.Generators.Configuration;

/// <summary>
/// Holds the configuration for hill elevation generation.
/// </summary>
/// <param name="HillCoverage">Fraction of map hexes to raise into hills (0.0–1.0).</param>
/// <param name="MaxElevation">Highest elevation level at the centre of a hill patch.</param>
/// <param name="Seed">Optional seed for a reproducible hill layout.</param>
public record LevelConfiguration(double HillCoverage, int MaxElevation, int? Seed);

