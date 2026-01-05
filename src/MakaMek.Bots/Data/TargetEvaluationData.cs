namespace Sanet.MakaMek.Bots.Data;

/// <summary>
/// Represents the score for a specific target with all configuration options
/// </summary>
public readonly record struct TargetEvaluationData
{
    public required Guid TargetId { get; init; }
    public required IReadOnlyList<WeaponConfigurationEvaluationData> ConfigurationScores { get; init; }
}