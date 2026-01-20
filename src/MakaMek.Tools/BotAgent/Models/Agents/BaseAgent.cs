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
        try
        {
            Logger.LogInformation("{AgentName} making decision for player {PlayerId}", Name, request.PlayerId);

            // Create an agent with MCP tools for this specific request
            var agent = await CreateAgentWithMcpTools(request, cancellationToken);
            
            // Call the specialized decision method
            return await GetAgentDecision(agent, request, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in {AgentName} decision making", Name);
            return CreateErrorResponse("AGENT_ERROR", ex.Message);
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
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The decision response</returns>
    protected abstract Task<DecisionResponse> GetAgentDecision(
        ChatClientAgent agent, 
        DecisionRequest request, 
        CancellationToken cancellationToken);

    /// <summary>
    /// Create a ChatClientAgent with MCP tools if available or without tools if MCP is not available.
    /// </summary>
    private async Task<ChatClientAgent> CreateAgentWithMcpTools(
        DecisionRequest request, 
        CancellationToken cancellationToken)
    {
        var tools = new List<AITool>();
        var mcpEndpoint = request.McpServerUrl;
        
        // Try to connect to the MCP server if URL is provided
        if (!string.IsNullOrEmpty(request.McpServerUrl))
        {
            try
            {
                await using var mcpClient = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions 
                {
                    TransportMode = HttpTransportMode.StreamableHttp,
                    Endpoint = new Uri(mcpEndpoint), 
                }), cancellationToken: cancellationToken);

                var toolsInMcp = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
                
                tools.AddRange(toolsInMcp);
                
                Logger.LogInformation("Successfully loaded {ToolCount} MCP tools from {McpUrl}", 
                    tools.Count, request.McpServerUrl);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to connect to MCP server at {McpUrl}, proceeding without tools", 
                    request.McpServerUrl);
                // Continue without MCP tools
            }
        }
        else
        {
            Logger.LogInformation("No MCP server URL provided, proceeding without tools");
        }

        return new ChatClientAgent(
            chatClient: LlmProvider.GetChatClient(),
            instructions: SystemPrompt,
            tools: tools
        );
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
