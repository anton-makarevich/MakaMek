using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

/// <summary>
/// Extensions that classify a physical attack declaration by where it was made.
/// </summary>
public static class PhysicalAttackDeclarationExtensions
{
    /// <param name="declaration">The declaration to inspect, if the unit has one.</param>
    extension(PhysicalAttackDeclarationData? declaration)
    {
        /// <summary>
        /// Whether the declaration is a movement-origin physical attack (Charge or DFA) rather than a
        /// physical attack declared in the Physical Attack Phase.
        /// </summary>
        public bool IsMovementAttack() =>
            declaration is { } attack
            && PhysicalAttackDefinitions.IsMovementAttack(attack.AttackType);

        /// <summary>
        /// Whether the declaration is a Death From Above. A pending DFA is the only state in which a
        /// unit is airborne during the Weapon Attack Phase (see docs/rules/physical-attacks.md).
        /// </summary>
        public bool IsDfa() => declaration is { AttackType: PhysicalAttackType.DFA };
    }
}
