namespace Sanet.MakaMek.Map.Data;

/// <summary>
/// Holds the configuration for hill elevation generation.
/// </summary>
public record LevelConfiguration
{
    public double HillCoverage { get; }
    public int MaxElevation { get; }
    public int? Seed { get; }

    public LevelConfiguration(double hillCoverage, int maxElevation, int? seed)
    {
        if (hillCoverage is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(hillCoverage), "HillCoverage must be between 0.0 and 1.0.");
        if (maxElevation < 1)
            throw new ArgumentOutOfRangeException(nameof(maxElevation), "MaxElevation must be at least 1.");

        HillCoverage = hillCoverage;
        MaxElevation = maxElevation;
        Seed = seed;
    }
}

