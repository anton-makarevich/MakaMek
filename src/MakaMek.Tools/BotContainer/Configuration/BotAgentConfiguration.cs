namespace Sanet.MakaMek.Tools.BotContainer.Configuration;

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
    /// Request timeout in milliseconds.
    /// </summary>
    public int Timeout { get; set; } = 30000;
}

