namespace Sanet.MakaMek.Hub.Contracts;

/// <summary>
/// Result of creating a relay room. Carries the Hub-minted device session identity
/// and the host game id; never carries player identity.
/// </summary>
public sealed record CreateRoomResponse(
    bool Success,
    string? RoomCode,
    Guid? DeviceSessionId,
    Guid? HostGameId,
    string? SessionToken,
    DateTimeOffset? ExpiresAt,
    HubError? Error);
