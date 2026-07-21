namespace Sanet.MakaMek.Hub.Rooms;

public sealed record RoomJoinResult(
    RoomJoinOutcome Outcome,
    Room? Room,
    RoomSession? Session)
{
    public static RoomJoinResult Joined(Room room, RoomSession session) =>
        new(RoomJoinOutcome.Joined, room, session);

    public static RoomJoinResult NotFound() =>
        new(RoomJoinOutcome.RoomNotFound, null, null);

    public static RoomJoinResult Expired() =>
        new(RoomJoinOutcome.RoomExpired, null, null);

    public static RoomJoinResult NotReady() =>
        new(RoomJoinOutcome.HostNotReady, null, null);

    public static RoomJoinResult HostPlayerIdConflict() =>
        new(RoomJoinOutcome.HostPlayerIdConflict, null, null);
}

public enum RoomJoinOutcome
{
    Joined,
    RoomNotFound,
    RoomExpired,
    HostNotReady,
    HostPlayerIdConflict
}
