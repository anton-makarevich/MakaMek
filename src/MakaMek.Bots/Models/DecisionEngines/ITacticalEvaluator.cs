using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Bots.Models.DecisionEngines;

public interface ITacticalEvaluator
{
    /// <summary>
    /// Evaluates a single path with a specific movement type and returns its score
    /// </summary>
    /// <param name="unit">The friendly unit being evaluated</param>
    /// <param name="path">The movement path to evaluate</param>
    /// <param name="enemyUnits">All enemy units</param>
    /// <returns>Position score including the path</returns>
    PositionScore EvaluatePath(
        IUnit unit,
        MovementPath path,
        IReadOnlyList<IUnit> enemyUnits);

    /// <summary>
    /// Evaluates potential targets for a unit
    /// </summary>
    /// <param name="attacker">The unit performing the attack</param>
    /// <param name="attackerPath">Movement path of the attacker to get to the position</param>
    /// <param name="potentialTargets">List of potential targets available</param>
    /// <returns>List of scores for each target</returns>
    List<TargetScore> EvaluateTargets(
        IUnit attacker,
        MovementPath attackerPath,
        IReadOnlyList<IUnit> potentialTargets);
}