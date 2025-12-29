using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Bots.Data;

/// <summary>
/// Detailed evaluation for a specific weapon against a target
/// </summary>
public readonly record struct WeaponEvaluationData
{
    public required Weapon Weapon { get; init; }
    public required double HitProbability { get; init; }
}