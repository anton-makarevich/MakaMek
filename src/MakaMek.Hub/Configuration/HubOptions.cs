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
    /// Maximum number of <c>Relay()</c> calls per minute per SignalR connection.
    /// </summary>
    public int RelayRateLimitPerMinute { get; init; } = 120;

    /// <summary>
    /// Maximum length of <see cref="Relay.RelayEnvelope.Payload"/> accepted by <c>Relay()</c>.
    /// </summary>
    public int MaxRelayPayloadBytes { get; init; } = 256 * 1024;

    /// <summary>
    /// Time-to-live in seconds for rooms. A room is garbage-collected after
    /// this duration of inactivity. Applies to all room states.
    /// </summary>
    public int RoomTtlSeconds { get; init; } = 7200;

    /// <summary>
    /// Grace period in seconds after the host disconnects before the room
    /// is permanently dissolved. Allows brief transport blips without
    /// destroying the session.
    /// </summary>
    public int DissolutionGracePeriodSeconds { get; init; } = 30;

    /// <summary>
    /// Trusted proxy CIDRs for ForwardedHeaders (comma-separated).
    /// </summary>
    public string[] TrustedProxies { get; init; } = [];
}
