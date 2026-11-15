using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// The target-side restriction a unit's own pending physical attack imposes on attacks against it
/// (Total Warfare p. 148-149). This is the single predicate for the "a unit performing X cannot be
/// the target of Y" rules; both the Punch/Kick/Push eligibility policy and the Charge/DFA movement
/// validator use it, so the per-attack scope cannot drift between them.
/// <list type="bullet">
/// <item>A 'Mech performing a Death From Above cannot be the target of any physical attack until
/// it lands.</item>
/// <item>A 'Mech performing a Charge is not immune to Punch or Kick, but cannot be pushed and
/// cannot be the target of another Charge or DFA.</item>
/// </list>
/// </summary>
public static class PhysicalAttackTargetExtensions
{
    /// <param name="target">The unit that may be the target of the attack.</param>
    extension(IUnit target)
    {
        /// <summary>
        /// The restriction the target's pending physical attack imposes on this attack type, or
        /// <c>null</c> when the target may be attacked.
        /// </summary>
        public PhysicalAttackRestriction? GetPendingAttackRestriction(PhysicalAttackType attackType) =>
            target.DeclaredPhysicalAttack switch
            {
                null => null,
                { AttackType: PhysicalAttackType.DFA } =>
                    PhysicalAttackRestriction.TargetPerformingDeathFromAbove,
                { AttackType: PhysicalAttackType.Charge }
                    when attackType is PhysicalAttackType.Push
                        or PhysicalAttackType.Charge or PhysicalAttackType.DFA =>
                    PhysicalAttackRestriction.TargetPerformingDisplacementAttack,
                _ => null
            };
    }
}
