namespace Sanet.MakaMek.Hub.Rooms;

/// <summary>
/// Opaque session credentials bound to one device session.
/// </summary>
public sealed record RoomSession(
    string Token,
    string RoomCode,
    Guid DeviceSessionId,
    RoomRole Role,
    DateTimeOffset ExpiresAt);
