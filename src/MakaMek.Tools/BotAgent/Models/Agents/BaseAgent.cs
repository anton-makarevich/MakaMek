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
    /// Build user prompt with game context. Must be implemented by specialized agents.
    /// </summary>
    protected abstract string BuildUserPrompt(DecisionRequest request);

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
