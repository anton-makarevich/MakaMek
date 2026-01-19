using MakaMek.Tools.BotContainer.Services;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace MakaMek.Tools.BotContainer.DecisionEngines;

/// <summary>
/// LLM-enabled weapons attack decision engine that calls BotAgent API
/// and falls back to the standard WeaponsEngine on failure.
/// </summary>
public class LlmWeaponsEngine : IBotDecisionEngine
{
    private readonly BotAgentClient _botAgentClient;
    private readonly WeaponsEngine _fallbackEngine;
    private readonly IClientGame _clientGame;
    private readonly string _mcpServerUrl;
    private readonly ILogger<LlmWeaponsEngine> _logger;

    public LlmWeaponsEngine(
        BotAgentClient botAgentClient,
        WeaponsEngine fallbackEngine,
        IClientGame clientGame,
        string mcpServerUrl,
        ILogger<LlmWeaponsEngine> logger)
    {
        _botAgentClient = botAgentClient;
        _fallbackEngine = fallbackEngine;
        _clientGame = clientGame;
        _mcpServerUrl = mcpServerUrl;
        _logger = logger;
    }

    public async Task MakeDecision(IPlayer player, ITurnState? turnState = null)
    {
        try
        {
            _logger.LogInformation(
                "LlmWeaponsEngine: Requesting decision from BotAgent for player {PlayerName}",
                player.Name);

            // Request decision from BotAgent
            var response = await _botAgentClient.RequestDecisionAsync(
                playerId: player.Id,
                phase: nameof(PhaseNames.WeaponsAttack),
                mcpServerUrl: _mcpServerUrl);

            // Check if we should use fallback
            if (!response.Success || response.FallbackRequired || response.Command == null)
            {
                _logger.LogWarning(
                    "LlmWeaponsEngine: BotAgent returned error or fallback required. " +
                    "ErrorType: {ErrorType}, ErrorMessage: {ErrorMessage}. Using fallback engine.",
                    response.ErrorType,
                    response.ErrorMessage);

                await _fallbackEngine.MakeDecision(player, turnState);
                return;
            }

            // Validate that the command is the correct type
            if (response.Command is not IClientCommand clientCommand)
            {
                _logger.LogWarning(
                    "LlmWeaponsEngine: BotAgent returned invalid command type. Using fallback engine.");
                await _fallbackEngine.MakeDecision(player, turnState);
                return;
            }

            _logger.LogInformation(
                "LlmWeaponsEngine: Received decision from BotAgent. " +
                "CommandType: {CommandType}, Reasoning: {Reasoning}",
                clientCommand.GetType().Name,
                response.Reasoning);

            // Execute the command based on its type
            switch (clientCommand)
            {
                case WeaponAttackDeclarationCommand attackCommand:
                    await _clientGame.DeclareWeaponAttack(attackCommand);
                    break;

                case WeaponConfigurationCommand configCommand:
                    await _clientGame.ConfigureUnitWeapons(configCommand);
                    break;

                default:
                    _logger.LogWarning(
                        "LlmWeaponsEngine: Unexpected command type {CommandType} for WeaponsAttack phase. Using fallback engine.",
                        clientCommand.GetType().Name);
                    await _fallbackEngine.MakeDecision(player, turnState);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "LlmWeaponsEngine: Error making decision for player {PlayerName}. Using fallback engine.",
                player.Name);

            await _fallbackEngine.MakeDecision(player, turnState);
        }
    }
}

