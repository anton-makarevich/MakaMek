using System.Text;
using BotAgent.Models;
using BotAgent.Services;
using Microsoft.Agents.AI;

namespace BotAgent.Agents;

/// <summary>
/// Abstract base class for all specialized agents using Microsoft Agent Framework.
/// </summary>
public abstract class BaseAgent : ISpecializedAgent
{
    protected ChatClientAgent Agent { get; init; }
    protected ILogger Logger { get; init; }
    protected McpClientService McpClient { get; init; }

    public abstract string Name { get; }
    public abstract string Description { get; }
    protected abstract string SystemPrompt { get; }

    protected BaseAgent(
        ILlmProvider llmProvider,
        McpClientService mcpClient,
        ILogger logger)
    {
        McpClient = mcpClient;
        Logger = logger;

        // CRITICAL: Must be ChatClientAgent for structured output support
        Agent = new ChatClientAgent(
            chatClient: llmProvider.GetChatClient(),
            instructions: SystemPrompt
        );
    }

    /// <summary>
    /// Make a decision using the agent. Can be overridden by specialized agents
    /// that use structured output.
    /// </summary>
    public virtual async Task<DecisionResponse> MakeDecisionAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("{AgentName} making decision for player {PlayerId}", Name, request.PlayerId);

            // Build user prompt with game context from DecisionRequest
            var userPrompt = BuildUserPrompt(request);

            // Run agent (basic RunAsync, not structured)
            var response = await Agent.RunAsync(userPrompt, cancellationToken: cancellationToken);

            var responseText = response.ToString();

            Logger.LogInformation("{AgentName} received response", Name);

            return new DecisionResponse(
                Success: true,
                Command: null, // Will be set by specialized agents
                Reasoning: responseText,
                ErrorType: null,
                ErrorMessage: null,
                FallbackRequired: false
            );
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in {AgentName} decision making", Name);
            return CreateErrorResponse("AGENT_ERROR", ex.Message);
        }
    }

    /// <summary>
    /// Build user prompt with game context. Can be overridden by specialized agents.
    /// </summary>
    protected virtual string BuildUserPrompt(DecisionRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Make a tactical decision for player {request.PlayerId} in phase {request.Phase}.");
        sb.AppendLine();

        // Add controlled units information
        if (request.ControlledUnits != null && request.ControlledUnits.Count > 0)
        {
            sb.AppendLine("YOUR UNITS:");
            foreach (var unit in request.ControlledUnits)
            {
                var deployStatus = unit.Position != null ? "DEPLOYED" : "UNDEPLOYED";
                sb.AppendLine($"- {unit.Model} ({unit.Mass} tons) - {deployStatus}");
                if (unit.Id.HasValue)
                    sb.AppendLine($"  ID: {unit.Id.Value}");
            }
            sb.AppendLine();
        }

        // Add enemy units information
        if (request.EnemyUnits != null && request.EnemyUnits.Count > 0)
        {
            sb.AppendLine("ENEMY UNITS:");
            foreach (var enemy in request.EnemyUnits)
            {
                sb.AppendLine($"- {enemy.Model} ({enemy.Mass} tons)");
                if (enemy.Position != null)
                    sb.AppendLine($"  Position: Q={enemy.Position.Q}, R={enemy.Position.R}");
            }
            sb.AppendLine();
        }

        // Add specific unit to deploy if specified
        if (request.UnitToAct.HasValue)
        {
            sb.AppendLine($"DEPLOY UNIT: {request.UnitToAct.Value}");
            sb.AppendLine();
        }

        sb.AppendLine("Select the best deployment position and facing direction based on tactical principles.");

        return sb.ToString();
    }

    protected DecisionResponse CreateErrorResponse(string errorType, string errorMessage)
    {
        return new DecisionResponse(
            Success: false,
            Command: null,
            Reasoning: null,
            ErrorType: errorType,
            ErrorMessage: errorMessage,
            FallbackRequired: true
        );
    }
}
