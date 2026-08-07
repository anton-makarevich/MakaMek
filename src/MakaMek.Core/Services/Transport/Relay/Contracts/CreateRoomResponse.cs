namespace Sanet.MakaMek.Core.Services.Transport.Relay.Contracts;

/// <summary>
/// Wire DTO returned by <c>POST api/rooms</c>.
/// </summary>
public sealed record CreateRoomResponse(
    bool Success,
    string? RoomCode,
    Guid? DeviceSessionId,
    Guid? HostGameId,
    string? SessionToken,
    DateTimeOffset? ExpiresAt,
    HubError? Error);
