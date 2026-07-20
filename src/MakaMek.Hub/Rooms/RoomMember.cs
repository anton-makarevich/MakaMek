namespace Sanet.MakaMek.Hub.Rooms;

/// <summary>
/// An anonymous player known to a room.
/// </summary>
public sealed record RoomMember(
    Guid PlayerId,
    string PlayerName,
    RoomRole Role,
    DateTimeOffset JoinedAt);
