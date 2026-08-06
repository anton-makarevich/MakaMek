namespace Sanet.MakaMek.Hub.Rooms;

/// <summary>
/// An authenticated device session known to a room. Carries no player identity:
/// the Hub never sees who is behind a device, only the Hub-minted session identity.
/// Connection routing state lives in <see cref="Room"/> keyed by <see cref="DeviceSessionId"/>.
/// </summary>
public sealed record RoomMember(
    Guid DeviceSessionId,
    RoomRole Role,
    DateTimeOffset JoinedAt);
