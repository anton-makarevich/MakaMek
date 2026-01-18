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
    /// Sampling temperature (0.0 - 2.0).
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Maximum tokens in response.
    /// </summary>
    public int MaxTokens { get; set; } = 2000;

    /// <summary>
    /// Request timeout in milliseconds.
    /// </summary>
    public int Timeout { get; set; } = 30000;

    /// <summary>
    /// API key for the LLM provider (should be loaded from environment variable).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
