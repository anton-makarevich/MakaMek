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
        /// hex is missing. The in-water flag is only true when the unit occupies the ground
        /// surface of a hex that has water; a bridge is never in water.
        /// </summary>
        public PhysicalAttackEndpoint? GetPhysicalAttackEndpoint(
            HexPosition position, int unitHeight)
        {
            var hex = map.GetHex(position.Coordinates);
            if (hex is null) return null;

            var (effectiveStandingLevel, isSubmerged) =
                hex.GetLosEndpointInfo(position.Surface, unitHeight);

            return new PhysicalAttackEndpoint(
                effectiveStandingLevel,
                isSubmerged,
                position.Surface == HexSurface.Ground && hex.GetWaterDepth() is > 0);
        }
    }
}
