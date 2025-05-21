using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Detailed breakdown of GATOR modifiers affecting an attack
/// </summary>
public record ToHitBreakdown
{
    /// <summary>
    /// Value representing an impossible roll
    /// </summary>
    public const int ImpossibleRoll = 13;

    /// <summary>
    /// Base gunnery skill of the attacker
    /// </summary>
    public required GunneryRollModifier GunneryBase { get; init; }

    /// <summary>
    /// Modifier based on attacker's movement type
    /// </summary>
    public required AttackerMovementModifier AttackerMovement { get; init; }

    /// <summary>
    /// Modifier based on target's movement distance
    /// </summary>
    public required TargetMovementModifier TargetMovement { get; init; }

    /// <summary>
    /// List of other modifiers with descriptions
    /// </summary>
    public required IReadOnlyList<RollModifier> OtherModifiers { get; init; }

    /// <summary>
    /// Modifier based on weapon range to target
    /// </summary>
    public required RangeRollModifier RangeModifier { get; init; }

    /// <summary>
    /// List of terrain modifiers along the line of sight
    /// </summary>
    public required IReadOnlyList<TerrainRollModifier> TerrainModifiers { get; init; }

    /// <summary>
    /// Whether there is a clear line of sight to the target
    /// </summary>
    public required bool HasLineOfSight { get; init; }

    /// <summary>
    /// All modifiers combined into a single list
    /// </summary>
    public IReadOnlyList<RollModifier> AllModifiers => new RollModifier[]
    {
        GunneryBase,
        AttackerMovement,
        TargetMovement,
        RangeModifier
    }.Concat(OtherModifiers)
     .Concat(TerrainModifiers)
     .ToList();

    /// <summary>
    /// Total modifier for the attack
    /// </summary>
    public int Total => HasLineOfSight ? 
        AllModifiers.Sum(m => m.Value)
        : ImpossibleRoll; // Cannot hit if no line of sight
}
