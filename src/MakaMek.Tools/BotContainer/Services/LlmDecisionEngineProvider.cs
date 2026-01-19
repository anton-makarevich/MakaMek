using MakaMek.Tools.BotContainer.DecisionEngines;
using Microsoft.Extensions.Options;
using MakaMek.Tools.BotContainer.Configuration;
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
        IOptions<BotAgentConfiguration> botAgentConfig,
        ILoggerFactory loggerFactory)
    {
        var config = botAgentConfig.Value;
        var tacticalEvaluator = new TacticalEvaluator(clientGame);

        // Create standard fallback engines
        var deploymentFallback = new DeploymentEngine(clientGame);
        var movementFallback = new MovementEngine(clientGame, tacticalEvaluator);
        var weaponsFallback = new WeaponsEngine(clientGame, tacticalEvaluator);
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
                    config.McpServerUrl,
                    loggerFactory.CreateLogger<LlmDeploymentEngine>())
            },
            {
                PhaseNames.Movement,
                new LlmMovementEngine(
                    botAgentClient,
                    movementFallback,
                    clientGame,
                    config.McpServerUrl,
                    loggerFactory.CreateLogger<LlmMovementEngine>())
            },
            {
                PhaseNames.WeaponsAttack,
                new LlmWeaponsEngine(
                    botAgentClient,
                    weaponsFallback,
                    clientGame,
                    config.McpServerUrl,
                    loggerFactory.CreateLogger<LlmWeaponsEngine>())
            },
            {
                PhaseNames.End,
                new LlmEndPhaseEngine(
                    botAgentClient,
                    endPhaseFallback,
                    clientGame,
                    config.McpServerUrl,
                    loggerFactory.CreateLogger<LlmEndPhaseEngine>())
            }
        };
    }

    public IBotDecisionEngine? GetEngineForPhase(PhaseNames phase)
    {
        return _decisionEngines.GetValueOrDefault(phase);
    }
}

