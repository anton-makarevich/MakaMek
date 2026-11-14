using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Evaluation;

/// <summary>
/// Scores the offensive value of an attacker position against a single target.
/// </summary>
public interface IOffensiveValueEvaluator
{
    /// <summary>
    /// Evaluates every weapon configuration of <paramref name="attacker"/> against a target.
    /// </summary>
    /// <param name="attacker">Snapshot of the attacking unit at the evaluated position.</param>
    /// <param name="attackerPath">Path the attacker would take to the evaluated position.</param>
    /// <param name="target">Snapshot of the target unit.</param>
    /// <returns>
    /// The per-configuration scores for the target, or <c>null</c> when there is no line of sight
    /// or no weapon configuration can contribute.
    /// </returns>
    TargetEvaluationData? Evaluate(UnitSnapshot attacker, MovementPath attackerPath, UnitSnapshot target);
}
