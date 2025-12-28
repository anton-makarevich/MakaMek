namespace Sanet.MakaMek.Bots.Data;

/// <summary>
/// Represents the score for a specific target
/// </summary>
public readonly record struct TargetScore
{
    public required Guid TargetId { get; init; }
    public required double Score { get; init; }
}
