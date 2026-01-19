using MakaMek.Tools.BotContainer.Services;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace MakaMek.Tools.BotContainer.DecisionEngines;

/// <summary>
/// LLM-enabled end phase decision engine that calls BotAgent API
/// and falls back to the standard EndPhaseEngine on failure.
/// </summary>
public class LlmEndPhaseEngine : LlmDecisionEngine<EndPhaseEngine>
{
    public LlmEndPhaseEngine(
        BotAgentClient botAgentClient,
        EndPhaseEngine fallbackEngine,
        IClientGame clientGame,
        string mcpServerUrl,
        ILogger<LlmEndPhaseEngine> logger)
        : base(botAgentClient, fallbackEngine, clientGame, mcpServerUrl, logger)
    {
    }

    protected override string PhaseName => nameof(PhaseNames.End);

    protected override async Task ExecuteCommandAsync(IClientCommand clientCommand, IPlayer player, ITurnState? turnState)
    {
        switch (clientCommand)
        {
            case ShutdownUnitCommand shutdownCommand:
                await ClientGame.ShutdownUnit(shutdownCommand);
                break;

            case StartupUnitCommand startupCommand:
                await ClientGame.StartupUnit(startupCommand);
                break;

            case TurnEndedCommand turnEndedCommand:
                await ClientGame.EndTurn(turnEndedCommand);
                break;

            default:
                Logger.LogWarning(
                    "LlmEndPhaseEngine: Unexpected command type {CommandType} for End phase. Using fallback engine.",
                    clientCommand.GetType().Name);
                await FallbackEngine.MakeDecision(player, turnState);
                break;
        }
    }
}

