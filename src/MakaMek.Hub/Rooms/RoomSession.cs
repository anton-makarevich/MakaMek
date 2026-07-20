namespace Sanet.MakaMek.Hub.Rooms;

/// <summary>
/// Opaque session credentials bound to one room member.
/// </summary>
public sealed record RoomSession(
    string Token,
    string RoomCode,
    Guid PlayerId,
    RoomRole Role,
    DateTimeOffset ExpiresAt);
