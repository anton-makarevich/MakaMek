using MakaMek.Tools.BotContainer.Models.DecisionEngines;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Bots.Services;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;

namespace MakaMek.Tools.BotContainer.Services;

/// <summary>
/// Provides LLM-enabled decision engines for specific game phases.
/// Each LLM engine wraps a standard fallback engine.
/// </summary>
public class LlmDecisionEngineProvider : IDecisionEngineProvider
{
    private readonly Dictionary<PhaseNames, IBotDecisionEngine> _decisionEngines;

    public LlmDecisionEngineProvider(
        IClientGame clientGame,
        BotAgentClient botAgentClient,
        string mcpServerUrl,
        ILoggerFactory loggerFactory)
    {
        TacticalEvaluator = new TacticalEvaluator(clientGame);

        // Create standard fallback engines
        var deploymentFallback = new DeploymentEngine(clientGame);
        var movementFallback = new MovementEngine(clientGame, TacticalEvaluator);
        var weaponsFallback = new WeaponsEngine(clientGame, TacticalEvaluator);
        var endPhaseFallback = new EndPhaseEngine(clientGame);

        // Create LLM-enabled engines that wrap the fallback engines
        _decisionEngines = new Dictionary<PhaseNames, IBotDecisionEngine>
        {
            {
                PhaseNames.Deployment,
                new LlmDeploymentEngine(
                    botAgentClient,
                    deploymentFallback,
                    clientGame,
                    mcpServerUrl,
                    loggerFactory.CreateLogger<LlmDeploymentEngine>())
            },
            {
                PhaseNames.Movement,
                new LlmMovementEngine(
                    botAgentClient,
                    movementFallback,
                    clientGame,
                    mcpServerUrl,
                    loggerFactory.CreateLogger<LlmMovementEngine>())
            },
            {
                PhaseNames.WeaponsAttack,
                new LlmWeaponsEngine(
                    botAgentClient,
                    weaponsFallback,
                    clientGame,
                    mcpServerUrl,
                    loggerFactory.CreateLogger<LlmWeaponsEngine>())
            },
            {
                PhaseNames.End,
                new LlmEndPhaseEngine(
                    botAgentClient,
                    endPhaseFallback,
                    clientGame,
                    mcpServerUrl,
                    loggerFactory.CreateLogger<LlmEndPhaseEngine>())
            }
        };
    }

    public IBotDecisionEngine? GetEngineForPhase(PhaseNames phase)
    {
        return _decisionEngines.GetValueOrDefault(phase);
    }
    
    public ITacticalEvaluator TacticalEvaluator {  get; private set; }
}