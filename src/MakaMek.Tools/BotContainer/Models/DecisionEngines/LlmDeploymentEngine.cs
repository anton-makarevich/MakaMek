using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Tools.BotContainer.Services;

namespace Sanet.MakaMek.Tools.BotContainer.Models.DecisionEngines;

/// <summary>
/// LLM-enabled deployment decision engine that calls BotAgent API
/// and falls back to the standard DeploymentEngine on failure.
/// </summary>
public class LlmDeploymentEngine : LlmDecisionEngine<DeploymentEngine>
{
    public LlmDeploymentEngine(
        BotAgentClient botAgentClient,
        DeploymentEngine fallbackEngine,
        IClientGame clientGame,
        string mcpServerUrl,
        ILogger<LlmDeploymentEngine> logger)
        : base(botAgentClient, fallbackEngine, clientGame, mcpServerUrl, logger)
    {
    }

    protected override string PhaseName => nameof(PhaseNames.Deployment);

    protected override async Task ExecuteCommandAsync(IClientCommand clientCommand, IPlayer player, ITurnState? turnState)
    {
        switch (clientCommand)
        {
            case DeployUnitCommand deployCommand:
                await ClientGame.DeployUnit(deployCommand);
                break;

            default:
                Logger.LogWarning(
                    "LlmDeploymentEngine: Unexpected command type {CommandType} for Deployment phase. Using fallback engine.",
                    clientCommand.GetType().Name);
                await FallbackEngine.MakeDecision(player, turnState);
                break;
        }
    }
}

