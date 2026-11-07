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
    }
}
