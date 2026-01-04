using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Bots.Data;

/// <summary>
/// Represents the score for a specific weapon configuration against a target
/// </summary>
public readonly record struct WeaponConfigurationEvaluationData
{
    public required WeaponConfiguration Configuration { get; init; }
    public required double Score { get; init; }
    public required IReadOnlyList<WeaponEvaluationData> ViableWeapons { get; init; }
}