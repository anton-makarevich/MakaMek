using Sanet.MakaMek.Bots.Models.Map;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Evaluation;

/// <summary>
/// Threat evaluation for enemy weapons that can reach a defended position.
/// </summary>
public sealed class WeaponThreatEvaluator(
    ILineOfSightEvaluator lineOfSight,
    IHitProbabilityCalculator hitProbability) : IWeaponThreatEvaluator
{
    public IEnumerable<double> GetThreats(
        UnitSnapshot enemy,
        FiringArc targetArc,
        MovementPath defenderPath,
        int defenderHeight,
        bool defenderIsProne)
    {
        var enemyPosition = enemy.Position;
        if (enemyPosition == null
            || !lineOfSight.HasLineOfSight(
                enemyPosition,
                defenderPath.Destination,
                enemy.Height,
                defenderHeight))
            yield break;

        var enemyPath = enemy.MovementPath ?? MovementPath.CreateSingleSegmentPath(enemyPosition);
        var range = enemyPosition.Coordinates.DistanceTo(defenderPath.Destination.Coordinates);
        var arcMultiplier = targetArc.GetArcMultiplier();

        foreach (var weapon in enemy.Weapons)
        {
            // Check if the weapon can fire at this range (minimum range penalty is handled by ToHitCalculator)
            if (!weapon.CanReach(range))
                continue;

            // Check firing arc
            if (!weapon.IsInFiringArc(enemyPosition.Coordinates, defenderPath.Destination.Coordinates))
                continue;

            // Calculate hit probability using ToHitCalculator with full accuracy
            // For defensive calculation, we're the target. A pending movement attack enemy
            // (such as a DFA attacker) is at its current approach position; MovementTaken is
            // still used for its movement type.
            var probability = hitProbability.Calculate(
                enemy,
                enemyPath,
                enemyPosition,
                defenderPath,
                defenderPath.Destination,
                weapon,
                null,
                defenderHeight,
                defenderIsProne);

            // Calculate threat value
            yield return probability * weapon.Damage * arcMultiplier;
        }
    }
}
