using System.Text;
using BotAgent.Services;
using BotAgent.Services.LlmProviders;
using Microsoft.Agents.AI;

namespace BotAgent.Models.Agents;

/// <summary>
/// Abstract base class for all specialized agents using Microsoft Agent Framework.
/// </summary>
public abstract class BaseAgent : ISpecializedAgent
{
    protected ChatClientAgent Agent { get; init; }
    protected ILogger Logger { get; init; }
    protected McpClientService McpClient { get; init; }

    public abstract string Name { get; }
    protected abstract string SystemPrompt { get; }

    protected BaseAgent(
        ILlmProvider llmProvider,
        McpClientService mcpClient,
        ILogger logger)
    {
        McpClient = mcpClient;
        Logger = logger;

        Agent = new ChatClientAgent(
            chatClient: llmProvider.GetChatClient(),
            instructions: SystemPrompt
        );
    }

    /// <summary>
    /// Make a decision using the agent. Can be overridden by specialized agents
    /// that use structured output.
    /// </summary>
    public virtual Task<DecisionResponse> MakeDecisionAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateErrorResponse("NOT_IMPLEMENTED", "Not implemented"));
    }

    /// <summary>
    /// Build user prompt with game context. Can be overridden by specialized agents.
    /// </summary>
    protected string BuildUserPrompt(DecisionRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Make a tactical decision for player {request.PlayerId} in phase {request.Phase}.");
        sb.AppendLine();

        // Add controlled units information
        if (request.ControlledUnits.Count > 0)
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
        if (request.EnemyUnits.Count > 0)
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

        // Add a specific unit to deploy if specified
        if (request.UnitToAct.HasValue)
        {
            sb.AppendLine($"DEPLOY UNIT: {request.UnitToAct.Value}");
            sb.AppendLine();
        }

        sb.AppendLine($"Select the best action for the {request.Phase} phase based on tactical principles.");

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
