using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Evaluation;

/// <summary>
/// Scores how exposed a position is to enemy fire.
/// </summary>
public interface IDefensiveValueEvaluator
{
    /// <summary>
    /// Calculates the defensive threat index for a position.
    /// Considers enemy weapons that can target the position and their hit probabilities.
    /// </summary>
    /// <param name="defenderPath">The position to evaluate and the path used to reach it.</param>
    /// <param name="enemyUnits">Snapshots of all enemy units.</param>
    /// <param name="defenderHeight">Height of the defending unit in levels.</param>
    /// <param name="defenderIsProne">Whether the defending unit is prone.</param>
    /// <returns>Defensive threat index (lower is better) and the number of enemies in the rear arc.</returns>
    PathDefensiveScore Evaluate(
        MovementPath defenderPath,
        IReadOnlyList<UnitSnapshot> enemyUnits,
        int defenderHeight,
        bool defenderIsProne);
}
