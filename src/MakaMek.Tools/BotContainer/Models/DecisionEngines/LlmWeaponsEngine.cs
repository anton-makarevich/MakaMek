using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Tools.BotContainer.Services;

namespace Sanet.MakaMek.Tools.BotContainer.Models.DecisionEngines;

/// <summary>
/// LLM-enabled weapons attack decision engine that calls BotAgent API
/// and falls back to the standard WeaponsEngine on failure.
/// </summary>
public class LlmWeaponsEngine : LlmDecisionEngine<WeaponsEngine>
{
    public LlmWeaponsEngine(
        BotAgentClient botAgentClient,
        WeaponsEngine fallbackEngine,
        IClientGame clientGame,
        string mcpServerUrl,
        ILogger<LlmWeaponsEngine> logger)
        : base(botAgentClient, fallbackEngine, clientGame, mcpServerUrl, logger)
    {
    }

    protected override string PhaseName => nameof(PhaseNames.WeaponsAttack);

    protected override async Task ExecuteCommandAsync(IClientCommand clientCommand, IPlayer player, ITurnState? turnState)
    {
        switch (clientCommand)
        {
            case WeaponAttackDeclarationCommand attackCommand:
                await ClientGame.DeclareWeaponAttack(attackCommand);
                break;

            case WeaponConfigurationCommand configCommand:
                await ClientGame.ConfigureUnitWeapons(configCommand);
                break;

            default:
                Logger.LogWarning(
                    "LlmWeaponsEngine: Unexpected command type {CommandType} for WeaponsAttack phase. Using fallback engine.",
                    clientCommand.GetType().Name);
                await FallbackEngine.MakeDecision(player, turnState);
                break;
        }
    }
}

