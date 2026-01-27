using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Tools.BotContainer.Services;

namespace Sanet.MakaMek.Tools.BotContainer.Models.DecisionEngines;

/// <summary>
/// LLM-enabled movement decision engine that calls BotAgent API
/// and falls back to the standard MovementEngine on failure.
/// </summary>
public class LlmMovementEngine : LlmDecisionEngine<MovementEngine>
{
    public LlmMovementEngine(
        BotAgentClient botAgentClient,
        MovementEngine fallbackEngine,
        IClientGame clientGame,
        string mcpServerUrl,
        ILogger<LlmMovementEngine> logger)
        : base(botAgentClient, fallbackEngine, clientGame, mcpServerUrl, logger)
    {
    }

    protected override string PhaseName => nameof(PhaseNames.Movement);

    protected override async Task ExecuteCommandAsync(IClientCommand clientCommand, IPlayer player, ITurnState? turnState)
    {
        switch (clientCommand)
        {
            case MoveUnitCommand moveCommand:
                await ClientGame.MoveUnit(moveCommand);
                break;

            case TryStandupCommand standUpCommand:
                await ClientGame.TryStandupUnit(standUpCommand);
                break;

            default:
                Logger.LogWarning(
                    "LlmMovementEngine: Unexpected command type {CommandType} for Movement phase. Using fallback engine.",
                    clientCommand.GetType().Name);
                await FallbackEngine.MakeDecision(player, turnState);
                break;
        }
    }
}

