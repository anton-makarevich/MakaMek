namespace MakaMek.Tools.BotContainer.Configuration;

/// <summary>
/// Configuration for BotAgent API integration.
/// </summary>
public class BotAgentConfiguration
{
    /// <summary>
    /// The URL of the BotAgent API (e.g., "http://localhost:5244").
    /// </summary>
    public string ApiUrl { get; set; } = "http://localhost:5244";

    /// <summary>
    /// The URL of this bot's MCP server for game state queries.
    /// This will be passed to the BotAgent so it can query game state.
    /// </summary>
    public string McpServerUrl { get; set; } = "http://localhost:5002/mcp";

    /// <summary>
    /// Request timeout in milliseconds.
    /// </summary>
    public int Timeout { get; set; } = 30000;
}

