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

    /// <summary>
    /// Maximum number of join attempts per minute per IP address.
    /// </summary>
    public int JoinRateLimitPerMinute { get; init; } = 10;

    /// <summary>
    /// Trusted proxy CIDRs for ForwardedHeaders (comma-separated).
    /// </summary>
    public string[] TrustedProxies { get; init; } = [];
}
