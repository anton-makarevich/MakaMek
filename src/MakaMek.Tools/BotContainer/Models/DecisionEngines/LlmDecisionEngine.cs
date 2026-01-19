using MakaMek.Tools.BotContainer.Services;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using BotAgent.Models;

namespace MakaMek.Tools.BotContainer.Models.DecisionEngines;

/// <summary>
/// Abstract base class for LLM-enabled decision engines that calls BotAgent API
/// and falls back to the standard engine on failure.
/// </summary>
public abstract class LlmDecisionEngine<TFallbackEngine> : IBotDecisionEngine 
    where TFallbackEngine : IBotDecisionEngine
{
    private readonly BotAgentClient _botAgentClient;
    private readonly string _mcpServerUrl;
    private readonly ILogger<LlmDecisionEngine<TFallbackEngine>> _logger;

    protected TFallbackEngine FallbackEngine { get; }

    protected IClientGame ClientGame { get; }

    protected ILogger Logger => _logger;

    protected LlmDecisionEngine(
        BotAgentClient botAgentClient,
        TFallbackEngine fallbackEngine,
        IClientGame clientGame,
        string mcpServerUrl,
        ILogger<LlmDecisionEngine<TFallbackEngine>> logger)
    {
        _botAgentClient = botAgentClient;
        FallbackEngine = fallbackEngine;
        ClientGame = clientGame;
        _mcpServerUrl = mcpServerUrl;
        _logger = logger;
    }

    public async Task MakeDecision(IPlayer player, ITurnState? turnState = null)
    {
        try
        {
            _logger.LogInformation(
                "{EngineType}: Requesting decision from BotAgent for player {PlayerName}",
                GetType().Name,
                player.Name);

            // Create decision request with game state
            var request = CreateDecisionRequest(player, turnState);

            // Request decision from BotAgent
            var response = await _botAgentClient.RequestDecisionAsync(request);

            // Check if we should use fallback
            if (!response.Success || response.FallbackRequired || response.Command == null)
            {
                _logger.LogWarning(
                    "{EngineType}: BotAgent returned error or fallback required. " +
                    "ErrorType: {ErrorType}, ErrorMessage: {ErrorMessage}. Using fallback engine.",
                    GetType().Name,
                    response.ErrorType,
                    response.ErrorMessage);

                await FallbackEngine.MakeDecision(player, turnState);
                return;
            }

            // Validate that the command is the correct type
            if (response.Command is not IClientCommand clientCommand)
            {
                _logger.LogWarning(
                    "{EngineType}: BotAgent returned invalid command type. Using fallback engine.",
                    GetType().Name);
                await FallbackEngine.MakeDecision(player, turnState);
                return;
            }

            _logger.LogInformation(
                "{EngineType}: Received decision from BotAgent. " +
                "CommandType: {CommandType}, Reasoning: {Reasoning}",
                GetType().Name,
                clientCommand.GetType().Name,
                response.Reasoning);

            // Execute the command based on its type
            await ExecuteCommandAsync(clientCommand, player, turnState);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "{EngineType}: Error making decision for player {PlayerName}. Using fallback engine.",
                GetType().Name,
                player.Name);

            await FallbackEngine.MakeDecision(player, turnState);
        }
    }

    /// <summary>
    /// Creates a DecisionRequest with game state for the BotAgent.
    /// Can be overridden by derived classes to add phase-specific data.
    /// </summary>
    /// <param name="player">The player making the decision</param>
    /// <param name="turnState">The current turn state</param>
    /// <returns>A populated DecisionRequest</returns>
    protected virtual DecisionRequest CreateDecisionRequest(IPlayer player, ITurnState? turnState)
    {
        // Get controlled units (player's units)
        var controlledUnits = ClientGame.Players
            .Where(p => p.Id == player.Id)
            .SelectMany(p => p.Units)
            .Select(u => u.ToData())
            .ToList();

        // Get enemy units (other players' units)
        var enemyUnits = ClientGame.Players
            .Where(p => p.Id != player.Id)
            .SelectMany(p => p.Units)
            .Select(u => u.ToData())
            .ToList();

        // Get specific unit to act (from turn state if available)
        var unitToAct = turnState?.ActiveUnitId;

        return new DecisionRequest(
            PlayerId: player.Id,
            Phase: PhaseName,
            McpServerUrl: _mcpServerUrl,
            Timeout: 30000,
            ControlledUnits: controlledUnits,
            EnemyUnits: enemyUnits,
            UnitToAct: unitToAct
        );
    }

    /// <summary>
    /// Gets the phase name for this decision engine.
    /// </summary>
    protected abstract string PhaseName { get; }

    /// <summary>
    /// Executes the appropriate command based on the client command type.
    /// </summary>
    /// <param name="clientCommand">The command to execute</param>
    /// <param name="player">The player making the decision</param>
    /// <param name="turnState">The current turn state</param>
    /// <returns>Task representing the async operation</returns>
    protected abstract Task ExecuteCommandAsync(IClientCommand clientCommand, IPlayer player, ITurnState? turnState);
}
