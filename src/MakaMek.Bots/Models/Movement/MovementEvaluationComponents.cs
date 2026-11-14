using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Core.Models.Game;

namespace Sanet.MakaMek.Bots.Models.Movement;

/// <summary>
/// Composition of the collaborators that <see cref="MovementEngine"/> delegates to.
/// </summary>
/// <param name="CandidateProvider">Generates the paths a unit can choose from.</param>
/// <param name="PriorityCalculator">Orders units by movement priority.</param>
/// <param name="PathPlanner">Scores candidates and selects the best path.</param>
/// <param name="CommandSender">Issues the movement commands a decision results in.</param>
public sealed record MovementEvaluationComponents(
    IMoveCandidateProvider CandidateProvider,
    IUnitPriorityCalculator PriorityCalculator,
    IMovementPathPlanner PathPlanner,
    IMovementCommandSender CommandSender)
{
    /// <summary>
    /// Wires the default movement evaluation graph for a game. This is the single place where the
    /// movement collaborators are constructed.
    /// </summary>
    public static MovementEvaluationComponents CreateDefault(
        IClientGame game,
        ITacticalEvaluator tacticalEvaluator,
        Random? random = null)
    {
        var candidateProvider = new MoveCandidateProvider(game);
        return new MovementEvaluationComponents(
            candidateProvider,
            new UnitPriorityCalculator(),
            new MovementPathPlanner(game, candidateProvider, tacticalEvaluator),
            new MovementCommandSender(game, random));
    }
}
