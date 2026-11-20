using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

public static class PhysicalAttackEndpointExtensions
{
    extension(PhysicalAttackEndpoint endpoint)
    {
        /// <summary>Signed raw level difference: <paramref name="to"/> minus this endpoint.</summary>
        public int LevelDifferenceTo(PhysicalAttackEndpoint to) =>
            to.EffectiveStandingLevel - endpoint.EffectiveStandingLevel;

        /// <summary>
        /// Whether the two endpoints respect the physical attack water line for this attack type
        /// (Total Warfare p. 151; issue #1600). This is the single water predicate: the Punch, Kick
        /// and Push eligibility policy and the Charge/DFA validator both use it, at declaration and
        /// at resolution, so the rule cannot drift.
        /// <list type="bullet">
        /// <item>A Charge keeps both endpoints on the same side of the water line.</item>
        /// <item>A DFA comes from above the surface, so neither endpoint may be submerged.</item>
        /// <item>A Kick occurs entirely underwater, so a Depth 1 attacker still reaches a submerged
        /// target.</item>
        /// <item>Punch and Push use the arms, so the attacker has to be submerged to reach a
        /// submerged target.</item>
        /// </list>
        /// </summary>
        public bool IsWaterEligibleAgainst(PhysicalAttackEndpoint target, PhysicalAttackType attackType) =>
            attackType switch
            {
                PhysicalAttackType.Charge => endpoint.IsSubmerged == target.IsSubmerged,
                PhysicalAttackType.DFA => !endpoint.IsSubmerged && !target.IsSubmerged,
                PhysicalAttackType.Kick => !target.IsSubmerged || endpoint.IsInWater,
                PhysicalAttackType.Punch or PhysicalAttackType.Push =>
                    !target.IsSubmerged || endpoint.IsSubmerged,
                _ => false
            };
    }
}
