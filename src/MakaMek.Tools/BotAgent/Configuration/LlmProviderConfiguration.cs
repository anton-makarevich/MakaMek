namespace BotAgent.Configuration;

/// <summary>
/// Configuration for LLM provider settings.
/// </summary>
public class LlmProviderConfiguration
{
    /// <summary>
    /// Provider type (OpenAI, Anthropic, AzureOpenAI).
    /// </summary>
    public string Type { get; set; } = "OpenAI";

    /// <summary>
    /// Model identifier (e.g., gpt-4o, claude-3-5-sonnet-20241022).
    /// </summary>
    public string Model { get; set; } = "gpt-4o";

    /// <summary>
    /// Request timeout in milliseconds.
    /// </summary>
    public int Timeout { get; set; } = 30000;

    /// <summary>
    /// API key for the LLM provider (should be loaded from an environment variable).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional endpoint for the LLM provider.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;
}
