using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

public static class PhysicalAttackEndpointExtensions
{
    extension(PhysicalAttackEndpoint endpoint)
    {
        /// <summary>Signed raw level difference: <paramref name="to"/> minus this endpoint.</summary>
        public int LevelDifferenceTo(PhysicalAttackEndpoint to) =>
            to.EffectiveStandingLevel - endpoint.EffectiveStandingLevel;
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
