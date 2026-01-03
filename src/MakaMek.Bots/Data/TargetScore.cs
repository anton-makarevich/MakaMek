namespace Sanet.MakaMek.Bots.Data;

/// <summary>
/// Represents the score for a specific target with all configuration options
/// </summary>
public readonly record struct TargetScore
{
    public required Guid TargetId { get; init; }
    public required IReadOnlyList<ConfigurationScore> ConfigurationScores { get; init; }
}