namespace Sanet.MakaMek.Hub.Configuration;

/// <summary>
/// Configuration for the relay hub's infrastructure limits and shared API key.
/// </summary>
public sealed class HubOptions
{
    public const string SectionName = "Hub";

    /// <summary>
    /// Shared key required by REST callers. It is intentionally supplied by deployment configuration.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// The maximum number of non-expired rooms the relay accepts at one time.
    /// </summary>
    public int MaxConcurrentRooms { get; init; } = 100;
}
