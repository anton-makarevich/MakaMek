using BotAgent.Models;
using BotAgent.Services;
using Microsoft.Agents.AI;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;

namespace BotAgent.Agents;

/// <summary>
/// Abstract base class for all specialized agents using Microsoft Agent Framework.
/// </summary>
public abstract class BaseAgent : ISpecializedAgent
{
    private readonly ChatClientAgent _agent;
    protected readonly McpClientService McpClient;
    private readonly ILogger _logger;

    public abstract string Name { get; }
    public abstract string Description { get; }
    protected abstract string SystemPrompt { get; }

    protected BaseAgent(
        ILlmProvider llmProvider,
        McpClientService mcpClient,
        ILogger logger)
    {
        McpClient = mcpClient;
        _logger = logger;

        // Initialize Microsoft Agent Framework ChatClientAgent
        _agent = new ChatClientAgent(
            chatClient: llmProvider.GetChatClient(),
            instructions: SystemPrompt
            // TODO: Register MCP tools here when implemented
            // tools: new [] { ... } 
        );
    }

    public async Task<DecisionResponse> MakeDecisionAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("{AgentName} making decision for player {PlayerId}", Name, request.PlayerId);

            // Build user prompt with game context
            // TODO: Enhance this with actual game state from MCP
            var userPrompt = $"Make a tactical decision for player {request.PlayerId} in phase {request.Phase}.";

            // Run agent
            // TODO: Use structured output or tool calls to get the command data
            var response = await _agent.RunAsync(userPrompt, cancellationToken: cancellationToken);
            
            var responseText = response.ToString(); // Or extract content properly
            
            // Parse response to command
            var command = ParseDecision(responseText, request);

            return CreateSuccessResponse(command, responseText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {AgentName} decision making", Name);
            return CreateErrorResponse("AGENT_ERROR", ex.Message);
        }
    }

    /// <summary>
    /// Parse the LLM response into a concrete command.
    /// </summary>
    protected abstract IClientCommand ParseDecision(string responseText, DecisionRequest request);

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

    protected DecisionResponse CreateSuccessResponse(IClientCommand command, string reasoning)
    {
        _logger.LogInformation("{AgentName} generated command: {CommandType}", Name, command.GetType().Name);

        return new DecisionResponse(
            Success: true,
            Command: command,
            Reasoning: reasoning,
            ErrorType: null,
            ErrorMessage: null,
            FallbackRequired: false
        );
    }
}
