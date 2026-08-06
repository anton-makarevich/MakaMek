namespace Sanet.MakaMek.Hub.Contracts;

/// <summary>
/// Result of joining a relay room. Carries the Hub-minted device session identity
/// and the host game id; never carries player identity.
/// </summary>
public sealed record JoinResponse(
    bool Success,
    string? Role,
    Guid? DeviceSessionId,
    Guid? HostGameId,
    string? SessionToken,
    HubError? Error);
