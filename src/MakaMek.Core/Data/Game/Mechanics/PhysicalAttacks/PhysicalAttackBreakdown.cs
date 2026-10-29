using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

/// <summary>
/// Detailed breakdown of modifiers affecting a physical attack. Unlike <see cref="ToHitBreakdown"/>
/// it is keyed on the attacker's Piloting Skill and has no weapon-only fields (range, firing arc).
/// </summary>
public record PhysicalAttackBreakdown
{
    /// <summary>
    /// Base piloting skill of the attacker
    /// </summary>
    public required PilotingRollModifier PilotingBase { get; init; }

    /// <summary>
    /// Per-type base modifier of the physical attack (Punch +0, Kick -2)
    /// </summary>
    public required PhysicalAttackBaseModifier AttackTypeModifier { get; init; }

    /// <summary>
    /// Modifier based on attacker's movement type
    /// </summary>
    public required AttackerMovementModifier AttackerMovement { get; init; }

    /// <summary>
    /// Modifier based on target's movement distance
    /// </summary>
    public required TargetMovementModifier TargetMovement { get; init; }

    /// <summary>
    /// List of terrain modifiers along the line of sight
    /// </summary>
    public required IReadOnlyList<TerrainRollModifier> TerrainModifiers { get; init; }

    /// <summary>
    /// List of other modifiers with descriptions. Holds target-side modifiers
    /// (partial cover +1, skidding target +2), the attacker-side skidding modifier (+1) and the
    /// per-damaged-shoulder Push modifier (+2 each), and is the extension point for
    /// ChargePilotingDifferenceModifier and DfaJumpModifier (M3).
    /// </summary>
    public required IReadOnlyList<RollModifier> OtherModifiers { get; init; }

    /// <summary>
    /// All modifiers combined into a single list, base first, in a fixed order
    /// </summary>
    public IReadOnlyList<RollModifier> AllModifiers =>
    [
        PilotingBase,
        AttackTypeModifier,
        AttackerMovement,
        TargetMovement, .. OtherModifiers,
        .. TerrainModifiers
    ];

    /// <summary>
    /// Total modifier for the attack
    /// </summary>
    public int Total => AllModifiers.Sum(m => m.Value);
}
