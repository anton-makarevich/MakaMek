using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Evaluation;

/// <summary>
/// Defensive threat evaluation that aggregates per-enemy weapon threat values.
/// </summary>
public sealed class DefensiveValueEvaluator(IWeaponThreatEvaluator weaponThreats) : IDefensiveValueEvaluator
{
    public PathDefensiveScore Evaluate(
        MovementPath defenderPath,
        IReadOnlyList<UnitSnapshot> enemyUnits,
        int defenderHeight,
        bool defenderIsProne)
    {
        double defensiveIndex = 0;
        var enemiesInRearArc = 0;
        var position = defenderPath.Destination;

        foreach (var enemy in enemyUnits)
        {
            var enemyPosition = enemy.Position;
            if (enemyPosition == null)
                continue;

            // Determine which arc of the friendly unit would be hit
            var targetArc = position.Coordinates.GetFiringArc(enemyPosition.Coordinates, position.Facing);
            if (targetArc == FiringArc.Rear)
                enemiesInRearArc++;

            foreach (var threatValue in weaponThreats.GetThreats(
                         enemy,
                         targetArc,
                         defenderPath,
                         defenderHeight,
                         defenderIsProne))
            {
                defensiveIndex += threatValue;
            }
        }

        return new PathDefensiveScore(defensiveIndex, enemiesInRearArc);
    }
}
