using BotAgent.Models;
using BotAgent.Services;

namespace BotAgent.Agents;

/// <summary>
/// Abstract base class for all specialized agents.
/// </summary>
public abstract class BaseAgent
{
    protected readonly ILlmProvider LlmProvider;
    protected readonly McpClientService McpClient;
    protected readonly ILogger Logger;

    /// <summary>
    /// Agent name for identification.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Agent description.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// System prompt defining agent role and instructions.
    /// </summary>
    protected abstract string SystemPrompt { get; }

    protected BaseAgent(
        ILlmProvider llmProvider,
        McpClientService mcpClient,
        ILogger logger)
    {
        LlmProvider = llmProvider;
        McpClient = mcpClient;
        Logger = logger;
    }

    /// <summary>
    /// Make a tactical decision for the given request.
    /// </summary>
    /// <param name="request">Decision request from Integration Bot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Decision response with command or error.</returns>
    public abstract Task<DecisionResponse> MakeDecisionAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handle errors and create error response.
    /// </summary>
    protected DecisionResponse CreateErrorResponse(string errorType, string errorMessage)
    {
        Logger.LogWarning("Agent {AgentName} error: {ErrorType} - {Message}", Name, errorType, errorMessage);

        return new DecisionResponse(
            Success: false,
            CommandType: null,
            Command: null,
            Reasoning: null,
            ErrorType: errorType,
            ErrorMessage: errorMessage,
            FallbackRequired: true
        );
    }

    /// <summary>
    /// Create success response with command.
    /// </summary>
    protected DecisionResponse CreateSuccessResponse(string commandType, object command, string reasoning)
    {
        Logger.LogInformation("Agent {AgentName} decision: {CommandType}", Name, commandType);

        return new DecisionResponse(
            Success: true,
            CommandType: commandType,
            Command: command,
            Reasoning: reasoning,
            ErrorType: null,
            ErrorMessage: null,
            FallbackRequired: false
        );
    }
}
