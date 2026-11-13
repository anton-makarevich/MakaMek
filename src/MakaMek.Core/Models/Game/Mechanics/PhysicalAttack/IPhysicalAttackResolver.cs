using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Resolves a physical attack for a single attacking limb. The resolver is pure mechanics: it
/// rolls the dice and calculates the damage, but it never mutates a unit, never creates a
/// command and never orchestrates a phase.
/// </summary>
public interface IPhysicalAttackResolver
{
    /// <summary>
    /// Resolves one physical attack limb against one target.
    /// </summary>
    /// <param name="context">The attacker, the target, the attack type, the attacking limb and the map.</param>
    /// <returns>
    /// The resolution data of the attack: hit or miss, the damage value, the hit location data
    /// and the piloting skill roll requirements the attack creates.
    /// </returns>
    /// <remarks>
    /// The method does not mutate any unit, does not create commands and does not validate
    /// eligibility, such as firing arcs, weapons fired this turn or actuator damage. Every call
    /// performs its own rolls, so a two-arm punch resolves as two independent calls.
    /// </remarks>
    PhysicalAttackResolutionData ResolveAttack(PhysicalAttackContext context);

    /// <summary>
    /// Resolves a charge against the unit in the committed destination hex. On a hit it calculates
    /// the target damage and the attacker's collision damage, both in 5-point clusters, and returns
    /// the piloting skill rolls the impact requires. On a miss it returns no damage and no rolls.
    /// </summary>
    /// <param name="context">The attacker, the selected target and the charge's committed data.</param>
    PhysicalAttackResolutionData ResolveCharge(ChargeAttackContext context);

    /// <summary>
    /// Resolves a Death From Above. On a hit it calculates the target damage on the Punch table
    /// (the rear column of the standard table for a prone target), the attacker's leg damage on
    /// the Kick table and returns the piloting skill rolls the impact requires. On a miss it
    /// returns no damage and no rolls.
    /// </summary>
    /// <param name="context">
    /// The attacker, the target and the movement origin the direction and push vector are measured
    /// from.
    /// </param>
    PhysicalAttackResolutionData ResolveDfa(DfaAttackContext context);
}
