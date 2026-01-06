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
    /// <param name="turnState">Optional turn state for caching evaluation results</param>
    /// <returns>Position score including the path</returns>
    Task<PositionScore> EvaluatePath(
        IUnit unit,
        MovementPath path,
        IReadOnlyList<IUnit> enemyUnits,
        ITurnState? turnState = null);

    /// <summary>
    /// Evaluates potential targets for a unit
    /// </summary>
    /// <param name="attacker">The unit performing the attack</param>
    /// <param name="attackerPath">Movement path of attacker to get to the position</param>
    /// <param name="potentialTargets">List of potential targets available</param>
    /// <param name="turnState">Optional turn state for caching evaluation results</param>
    /// <returns>List of scores for each target</returns>
    ValueTask<IReadOnlyList<TargetEvaluationData>> EvaluateTargets(
        IUnit attacker,
        MovementPath attackerPath,
        IReadOnlyList<IUnit> potentialTargets,
        ITurnState? turnState = null);
}