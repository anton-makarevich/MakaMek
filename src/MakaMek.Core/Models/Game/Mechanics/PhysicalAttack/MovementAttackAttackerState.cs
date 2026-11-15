namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// What is left of a declared Charge or DFA for its attacker at resolution time. The declaration
/// was validated when the move was processed; this state describes whether the attacker can still
/// carry it out after the rest of the turn.
/// </summary>
public enum MovementAttackAttackerState
{
    /// <summary>The attacker can still make the declared attack.</summary>
    CanAttack,

    /// <summary>
    /// The attack resolves as an automatic failure without a to-hit roll: the attacker cannot make
    /// it any more, either because it is destroyed, shut down, immobile or prone, or because it
    /// is no longer in the hex it committed to attack from. A charge keeps the attacker in place; a
    /// DFA grounds it in the hex it committed to land in.
    /// </summary>
    CannotAttack,

    /// <summary>
    /// The attacker's own state turns the attack into a miss without a to-hit roll. An immobile or
    /// prone DFA attacker still lands in the target hex and falls.
    /// </summary>
    MustMiss
}
