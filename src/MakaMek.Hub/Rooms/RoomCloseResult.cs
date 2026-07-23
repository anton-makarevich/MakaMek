namespace Sanet.MakaMek.Hub.Rooms;

public sealed record RoomCloseResult(RoomCloseOutcome Outcome)
{
    public static RoomCloseResult Closed() =>
        new(RoomCloseOutcome.Closed);

    public static RoomCloseResult NotFound() =>
        new(RoomCloseOutcome.RoomNotFound);

    public static RoomCloseResult Expired() =>
        new(RoomCloseOutcome.RoomExpired);

    public static RoomCloseResult NotHost() =>
        new(RoomCloseOutcome.NotHost);

    public static RoomCloseResult InvalidState() =>
        new(RoomCloseOutcome.InvalidRoomState);
}

public enum RoomCloseOutcome
{
    Closed,
    RoomNotFound,
    RoomExpired,
    NotHost,
    InvalidRoomState
}
