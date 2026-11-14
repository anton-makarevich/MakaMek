using System.Collections.Concurrent;
using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Bots.Models.Logger;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Movement;

/// <summary>
/// Evaluates candidate paths in parallel and selects the highest-scoring one.
/// </summary>
public sealed class MovementPathPlanner(
    IClientGame game,
    IMoveCandidateProvider candidateProvider,
    ITacticalEvaluator tacticalEvaluator) : IMovementPathPlanner
{
    private const int MaxConcurrency = 20;

    public MovementPath? SelectBestPath(
        IUnit unit,
        IReadOnlyList<IUnit> enemyUnits,
        IReadOnlySet<HexCoordinates> friendlyPositions,
        ITurnState? turnState,
        BotSettings settings)
    {
        var candidatePaths = candidateProvider.GetCandidatePaths(unit, friendlyPositions);

        // Evaluate each path with controlled concurrency
        var scores = new ConcurrentBag<PositionScore>();
        Parallel.ForEach(candidatePaths, new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrency },
            (path, _) =>
            {
                try
                {
                    var score = tacticalEvaluator.EvaluatePath(unit, path, enemyUnits, turnState);
                    scores.Add(score);
                }
                catch (Exception ex)
                {
                    // Log and continue - don't let one bad path evaluation stop all evaluations
                    game.Logger.LogFailedToEvaluatePath(
                        path.Destination.Coordinates,
                        ex.Message,
                        ex);
                }
            });

        // If no valid paths, stand still
        if (scores.IsEmpty)
            return null;

        // Select the path with the best combined score
        // Use aggressiveness index to create a smooth gradient between offensive and defensive preferences
        var bestScore = scores
            .Select(s => new
            {
                Score = s,
                CombinedScore = settings.AggressivenessIndex * s.OffensiveIndex
                                - (1 - settings.AggressivenessIndex) * s.DefensiveIndex
            })
            .OrderBy(x => x.Score.EnemiesInRearArc)
            .ThenByDescending(x => x.CombinedScore)
            .ThenByDescending(x => x.Score.Path.HexesTraveled)
            .Select(x => x.Score)
            .First(); // TODO: for different difficulty levels we can take random of first N

        game.Logger.LogUnitMovingToPositionUsingMovementTypeWithOffensiveDefensiveAndEnemiesInRearArc(
            unit.Name,
            bestScore.Position.Coordinates,
            bestScore.MovementType,
            bestScore.OffensiveIndex,
            bestScore.DefensiveIndex,
            bestScore.EnemiesInRearArc);

        return bestScore.Path;
    }
}
