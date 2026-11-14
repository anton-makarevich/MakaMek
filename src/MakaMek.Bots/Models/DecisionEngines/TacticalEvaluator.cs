using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Bots.Models.Evaluation;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.DecisionEngines;

/// <summary>
/// Evaluates tactical situations for movement and weapon decisions by delegating the scoring
/// concerns to the injected evaluation collaborators.
/// </summary>
public class TacticalEvaluator : ITacticalEvaluator
{
    private readonly IClientGame _game;
    private readonly IUnitSnapshotFactory _snapshots;
    private readonly IDefensiveValueEvaluator _defensiveValue;
    private readonly IOffensiveValueEvaluator _offensiveValue;

    /// <summary>
    /// Creates the evaluator with the default evaluation graph. Prefer the constructor that takes
    /// explicit <see cref="TacticalEvaluationComponents"/> when composing the bot.
    /// </summary>
    public TacticalEvaluator(IClientGame game)
        : this(game, TacticalEvaluationComponents.CreateDefault(game))
    {
    }

    public TacticalEvaluator(IClientGame game, TacticalEvaluationComponents components)
    {
        _game = game;
        _snapshots = components.Snapshots;
        _defensiveValue = components.DefensiveValue;
        _offensiveValue = components.OffensiveValue;
    }

    /// <summary>
    /// Evaluates a single path with a specific movement type and returns its score
    /// </summary>
    /// <param name="unit">The unit being evaluated</param>
    /// <param name="path">The movement path for the unit to evaluate</param>
    /// <param name="enemyUnits">All enemy units</param>
    /// <param name="turnState">Optional turn state for caching evaluation results</param>
    public PositionScore EvaluatePath(
        IUnit unit,
        MovementPath path,
        IReadOnlyList<IUnit> enemyUnits,
        ITurnState? turnState = null)
    {
        PathDefensiveScore defensiveScore;
        if (_game.BattleMap == null)
        {
            defensiveScore = new PathDefensiveScore(0, 0);
        }
        else
        {
            var enemySnapshots = enemyUnits.Select(_snapshots.Create).ToList();
            defensiveScore = _defensiveValue.Evaluate(
                path,
                enemySnapshots,
                unit.Height,
                unit.Status.HasFlag(UnitStatus.Prone));
        }

        var targetScores = EvaluateTargets(unit, path, enemyUnits, turnState);
        var offensiveIndex = targetScores
            .Sum(t => t.ConfigurationScores.Count > 0
                ? t.ConfigurationScores.Max(cs => cs.Score)
                : 0);

        return new PositionScore
        {
            Position = path.Destination,
            MovementType = path.MovementType,
            Path = path,
            DefensiveIndex = defensiveScore.Score,
            OffensiveIndex = offensiveIndex,
            EnemiesInRearArc = defensiveScore.EnemiesInRearArc
        };
    }

    /// <summary>
    /// Evaluates potential targets for a unit
    /// </summary>
    public IReadOnlyList<TargetEvaluationData> EvaluateTargets(
        IUnit attacker, MovementPath attackerPath, IReadOnlyList<IUnit> potentialTargets, ITurnState? turnState = null)
    {
        if (_game.BattleMap == null || attacker.Position == null)
            return [];

        var results = new List<TargetEvaluationData>();
        var attackerSnapshot = _snapshots.CreateForAttacker(attacker, attackerPath);

        foreach (var target in potentialTargets)
        {
            var targetSnapshot = _snapshots.Create(target);
            if (targetSnapshot.Position == null)
                continue;

            var key = new TargetEvaluationKey(
                attacker.Id, attackerPath.Destination.Coordinates, attackerPath.Destination.Facing,
                attackerPath.MovementType,
                targetSnapshot.Id, targetSnapshot.Position.Coordinates, targetSnapshot.Position.Facing,
                targetSnapshot.Position.AirborneLevel
            );

            if (turnState?.TryGetTargetEvaluation(key, out var cachedData) == true)
            {
                results.Add(cachedData);
                continue;
            }

            var evaluation = _offensiveValue.Evaluate(attackerSnapshot, attackerPath, targetSnapshot);
            if (evaluation == null)
                continue;

            results.Add(evaluation.Value);
            turnState?.AddTargetEvaluation(key, evaluation.Value);
        }

        return results;
    }
}
