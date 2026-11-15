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

    extension(IBattleMap map)
    {
        /// <summary>
        /// Resolves the endpoint from the map, not from <c>Unit.Hex</c>. Returns null when the
        /// hex is missing.
        /// <para>
        /// A pending Death From Above leaves the unit airborne above its approach hex. Its feet
        /// stand at the absolute airborne base level (<c>max(...) + 2</c>), so that level replaces
        /// the surface standing level for reach: physical attacks against the airborne unit face
        /// the real elevation difference (Total Warfare p. 149-150). An airborne endpoint is never
        /// submerged or in water.
        /// </para>
        /// The in-water flag is only true when the unit occupies the ground surface of a hex that
        /// has water; a bridge is never in water.
        /// </summary>
        public PhysicalAttackEndpoint? GetPhysicalAttackEndpoint(
            HexPosition position, int unitHeight)
        {
            var hex = map.GetHex(position.Coordinates);
            if (hex is null) return null;

            if (position.AirborneLevel is { } airborneBaseLevel)
                return new PhysicalAttackEndpoint(airborneBaseLevel, IsSubmerged: false, IsInWater: false);

            var (effectiveStandingLevel, isSubmerged) =
                hex.GetLosEndpointInfo(position.Surface, unitHeight);

            return new PhysicalAttackEndpoint(
                effectiveStandingLevel,
                isSubmerged,
                position.Surface == HexSurface.Ground && hex.GetWaterDepth() is > 0);
        }
    }
}
