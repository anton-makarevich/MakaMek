using BotAgent.Services.LlmProviders;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace BotAgent.Models.Agents;

/// <summary>
/// Abstract base class for all specialized agents using Microsoft Agent Framework.
/// </summary>
public abstract class BaseAgent : ISpecializedAgent
{
    protected ILogger Logger { get; init; }
    private ILlmProvider LlmProvider { get; init; }
    
    public abstract string Name { get; }
    protected abstract string SystemPrompt { get; }

    protected BaseAgent(
        ILlmProvider llmProvider,
        ILogger logger)
    {
        Logger = logger;
        LlmProvider = llmProvider;
    }

    /// <summary>
    /// Make a decision using the agent. This is the public entry point that handles
    /// MCP integration and calls the specialized GetAgentDecision method.
    /// </summary>
    public async Task<DecisionResponse> MakeDecision(
        DecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        McpClient? mcpClient = null;

        try
        {
            Logger.LogInformation("{AgentName} making decision for player {PlayerId}", Name, request.PlayerId);
            var mcpEndpoint = request.McpServerUrl;

            // Create an agent with MCP tools for this specific request
            mcpClient = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions 
            {
                TransportMode = HttpTransportMode.StreamableHttp,
                Endpoint = new Uri(mcpEndpoint), 
            }), cancellationToken: cancellationToken);

            var toolsInMcp = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
            var availableToolNames = toolsInMcp.Select(t => t.Name).ToArray();
            
            foreach (var tool in toolsInMcp)
            {
                Logger.LogInformation("Tool found: {ToolName}", tool.Name);
            }
            
            var agent =new ChatClientAgent(
                chatClient: LlmProvider.GetChatClient(),
                instructions: SystemPrompt,
                tools: toolsInMcp.Cast<AITool>().ToArray()
            );
            
            // Call the specialized decision method
            return await GetAgentDecision(agent, request, availableToolNames, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in {AgentName} decision making", Name);
            return CreateErrorResponse("AGENT_ERROR", ex.Message);
        }
        finally
        {
            if (mcpClient != null)
                await mcpClient.DisposeAsync();
        }
    }

    /// <summary>
    /// Build user prompt with game context. Must be implemented by specialized agents.
    /// </summary>
    protected abstract string BuildUserPrompt(DecisionRequest request);

    /// <summary>
    /// Make the actual decision using the provided agent. Must be implemented by specialized agents.
    /// </summary>
    /// <param name="agent">The agent instance with MCP tools (if available)</param>
    /// <param name="request">The decision request</param>
    /// <param name="availableTools">Names of available tools</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The decision response</returns>
    protected abstract Task<DecisionResponse> GetAgentDecision(
        ChatClientAgent agent, 
        DecisionRequest request, 
        string[] availableTools,
        CancellationToken cancellationToken);

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
