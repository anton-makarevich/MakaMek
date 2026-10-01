using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics;

/// <summary>
/// Breakdown of the modifiers on a physical attack. There is no field for heat or sensor damage,
/// which is how "they never apply" is enforced: there is nowhere to put them.
/// </summary>
public record PhysicalToHitBreakdown
{
    /// <summary>
    /// Value representing an impossible roll
    /// </summary>
    public const int ImpossibleRoll = 13;

    /// <summary>
    /// Attacker's piloting skill, the base for a physical attack
    /// </summary>
    public required PilotingRollModifier PilotingBase { get; init; }

    /// <summary>
    /// The per-type base: punch, kick and so on
    /// </summary>
    public required PhysicalAttackBaseModifier AttackTypeBase { get; init; }

    /// <summary>
    /// Modifier based on the attacker's movement type
    /// </summary>
    public required AttackerMovementModifier AttackerMovement { get; init; }

    /// <summary>
    /// Prone, skidding, actuator damage: whatever the unit reports that is not heat or sensors
    /// </summary>
    public required IReadOnlyList<RollModifier> OtherModifiers { get; init; }

    /// <summary>
    /// Terrain modifiers along the line of sight
    /// </summary>
    public required IReadOnlyList<TerrainRollModifier> TerrainModifiers { get; init; }

    public required bool HasLineOfSight { get; init; }

    public IReadOnlyList<RollModifier> AllModifiers => new RollModifier[]
        {
            PilotingBase,
            AttackTypeBase,
            AttackerMovement
        }.Concat(OtherModifiers)
        .Concat(TerrainModifiers)
        .ToList();

    /// <summary>
    /// Total to-hit number, or ImpossibleRoll with no line of sight.
    /// </summary>
    public int Total => HasLineOfSight ? AllModifiers.Sum(m => m.Value) : ImpossibleRoll;
}
