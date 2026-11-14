using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Plumbing shared by the physical attack resolvers: damage grouping, the weapon-shaped payload the
/// standard damage pipeline consumes, and the damage transfer lookup for a rolled hit location.
/// </summary>
internal static class PhysicalAttackRolls
{
    /// <summary>
    /// Splits damage into full 5-point groups plus a remainder, the grouping every physical attack
    /// uses for its hit location rolls.
    /// </summary>
    public static IEnumerable<int> SplitIntoFivePointGroups(int damage)
    {
        while (damage > 5)
        {
            yield return 5;
            damage -= 5;
        }

        if (damage > 0) yield return damage;
    }

    /// <summary>
    /// Builds the weapon-shaped payload that the standard damage pipeline consumes. A physical
    /// attack generates no heat, has no cluster roll or missiles, and a miss is always reported as
    /// coming from the front. Isolating the adapter here documents the boundary instead of
    /// scattering weapon-only values through the resolvers.
    /// </summary>
    public static AttackResolutionData CreateAttackResolution(
        int toHitNumber,
        List<DiceResult> attackRoll,
        bool isHit,
        HitDirection attackDirection,
        AttackHitLocationsData? hitLocationsData,
        HitDirection? damageDirection = null) =>
        new(
            toHitNumber,
            attackRoll,
            isHit,
            attackDirection,
            ExternalHeat: 0,
            HitLocationsData: hitLocationsData,
            DamageDirection: damageDirection);

    /// <summary>
    /// Follows the damage transfer chain of an already destroyed location, as the weapon
    /// resolver does, so a hit on a blown off limb lands on the next intact location.
    /// </summary>
    public static PartLocation ResolveTransferLocation(IUnit target, PartLocation rolledLocation)
    {
        var hitLocation = rolledLocation;
        var visited = new HashSet<PartLocation> { rolledLocation };

        while (target.Parts.TryGetValue(hitLocation, out var part) && part.IsDestroyed)
        {
            var nextLocation = part.GetNextTransferLocation();
            if (nextLocation == null || !visited.Add(nextLocation.Value))
                break;

            hitLocation = nextLocation.Value;
        }

        return hitLocation;
    }
}
