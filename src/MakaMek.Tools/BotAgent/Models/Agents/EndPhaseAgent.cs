using BotAgent.Services.LlmProviders;
using Microsoft.Agents.AI;

namespace BotAgent.Models.Agents;

/// <summary>
/// End phase agent - manages shutdown and startup decisions, ends turn.
/// </summary>
public class EndPhaseAgent : BaseAgent
{
    public override string Name => "EndPhaseAgent";

    protected override string SystemPrompt => """
        You are a BattleTech tactical AI specializing in end phase decisions.
        Consider shutting down overheated units (ShutdownUnitCommand).
        Consider restarting shutdown units (StartupUnitCommand).
        End turn (TurnEndedCommand) when all unit management is done.
        """;

    public EndPhaseAgent(
        ILlmProvider llmProvider,
        ILogger<EndPhaseAgent> logger)
        : base(llmProvider, logger)
    {
    }

    /// <summary>
    /// Build user prompt with game context for end-phase decisions.
    /// </summary>
    protected override string BuildUserPrompt(DecisionRequest request) => 
        throw new NotImplementedException("BuildUserPrompt not yet implemented for this agent");

    /// <summary>
    /// Make the actual end-phase decision using the provided agent.
    /// </summary>
    protected override Task<DecisionResponse> GetAgentDecision(
        AIAgent agent, 
        DecisionRequest request, 
        string[] availableTools,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(CreateErrorResponse("NOT_IMPLEMENTED", "EndPhaseAgent not yet implemented"));
    }
}
