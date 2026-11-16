using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Bots.Models.Evaluation;
using Sanet.MakaMek.Bots.Models.Movement;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;

namespace Sanet.MakaMek.Bots.Services;

/// <summary>
/// Provides decision engines for specific game phases
/// </summary>
public class DecisionEngineProvider : IDecisionEngineProvider
{
    private readonly Dictionary<PhaseNames, IBotDecisionEngine> _decisionEngines;

    public DecisionEngineProvider(IClientGame clientGame)
    {
        var tacticalEvaluator = new TacticalEvaluator(
            clientGame,
            TacticalEvaluationComponents.CreateDefault(clientGame));

        // Initialize decision engines for each phase (shared across all bots)
        _decisionEngines = new Dictionary<PhaseNames, IBotDecisionEngine>
        {
            { PhaseNames.Deployment, new DeploymentEngine(clientGame) },
            {
                PhaseNames.Movement,
                new MovementEngine(
                    clientGame,
                    MovementEvaluationComponents.CreateDefault(clientGame, tacticalEvaluator))
            },
            {
                PhaseNames.WeaponsAttack,
                new WeaponsEngine(
                    clientGame,
                    tacticalEvaluator,
                    WeaponsEvaluationComponents.CreateDefault(clientGame))
            },
            { PhaseNames.PhysicalAttack, new PhysicalAttackEngine(clientGame) },
            { PhaseNames.End, new EndPhaseEngine(clientGame) }
        };
    }

    public IBotDecisionEngine? GetEngineForPhase(PhaseNames phase)
    {
        return _decisionEngines.GetValueOrDefault(phase);
    }
}
