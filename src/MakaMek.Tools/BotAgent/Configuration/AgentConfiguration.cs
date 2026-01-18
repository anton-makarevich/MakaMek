namespace BotAgent.Configuration;

/// <summary>
/// Configuration for agent behavior settings.
/// </summary>
public class AgentConfiguration
{
    /// <summary>
    /// Enable chain-of-thought reasoning in agent responses.
    /// </summary>
    public bool EnableChainOfThought { get; set; } = true;

    /// <summary>
    /// Maximum number of MCP tool calls allowed per decision request.
    /// </summary>
    public int MaxMcpToolCalls { get; set; } = 10;
}
