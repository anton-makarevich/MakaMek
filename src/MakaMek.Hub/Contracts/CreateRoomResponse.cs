namespace Sanet.MakaMek.Hub.Contracts;

/// <summary>
/// Result of creating a relay room.
/// </summary>
public sealed record CreateRoomResponse(
    bool Success,
    string? RoomCode,
    Guid? HostId,
    string? SessionToken,
    DateTimeOffset? ExpiresAt,
    HubError? Error);
