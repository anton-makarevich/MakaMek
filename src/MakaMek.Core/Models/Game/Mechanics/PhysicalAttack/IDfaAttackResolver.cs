using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Resolves a Death From Above. Like <see cref="IPhysicalAttackResolver"/>, the resolver is pure
/// mechanics: it rolls the dice and calculates the damage, but it never mutates a unit, never
/// creates a command and never orchestrates a phase.
/// </summary>
public interface IDfaAttackResolver
{
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
