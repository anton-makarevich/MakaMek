using BotAgent.Models;
using BotAgent.Services;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;

namespace BotAgent.Agents;

/// <summary>
/// End phase agent - manages shutdown and startup decisions, ends turn.
/// </summary>
public class EndPhaseAgent : BaseAgent
{
    public override string Name => "EndPhaseAgent";
    public override string Description => "Specialist in heat management and end phase decisions";

    protected override string SystemPrompt => """
        You are a BattleTech tactical AI specializing in end phase decisions.
        Consider shutting down overheated units (ShutdownUnitCommand).
        Consider restarting shutdown units (StartupUnitCommand).
        End the turn (TurnEndedCommand) when all unit management is done.
        """;

    public EndPhaseAgent(
        ILlmProvider llmProvider,
        McpClientService mcpClient,
        ILogger<EndPhaseAgent> logger)
        : base(llmProvider, mcpClient, logger)
    {
    }

    protected override IClientCommand ParseDecision(string responseText, DecisionRequest request)
    {
        // TODO: Parse actual JSON response from LLM
        // TODO: Determine if we need to startup, shutdown, or end turn
        
        // Placeholder: Default to TurnEndedCommand
        return new TurnEndedCommand
        {
            PlayerId = request.PlayerId,
            GameOriginId = Guid.Empty, // From context
            Timestamp = DateTime.UtcNow,
            IdempotencyKey = Guid.NewGuid()
        };
    }
}
