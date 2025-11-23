using Sanet.MakaMek.Bots.DecisionEngines;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;

namespace Sanet.MakaMek.Bots.Services;

/// <summary>
/// Provides decision engines for specific game phases
/// </summary>
public class DecisionEngineProvider : IDecisionEngineProvider
{
    private readonly Dictionary<PhaseNames, IBotDecisionEngine> _decisionEngines;

    public DecisionEngineProvider(IClientGame clientGame, BotDifficulty difficulty)
    {
        // Initialize decision engines for each phase (shared across all bots)
        _decisionEngines = new Dictionary<PhaseNames, IBotDecisionEngine>
        {
            { PhaseNames.Deployment, new DeploymentEngine(clientGame, difficulty) },
            { PhaseNames.Movement, new MovementEngine(clientGame, difficulty) },
            { PhaseNames.WeaponsAttack, new WeaponsEngine(clientGame, difficulty) },
            { PhaseNames.End, new EndPhaseEngine(clientGame, difficulty) }
        };
    }

    public IBotDecisionEngine? GetEngineForPhase(PhaseNames phase)
    {
        return _decisionEngines.GetValueOrDefault(phase);
    }
}
