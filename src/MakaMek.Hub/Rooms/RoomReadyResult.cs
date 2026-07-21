namespace Sanet.MakaMek.Hub.Rooms;

public sealed record RoomReadyResult(RoomReadyOutcome Outcome)
{
    public static RoomReadyResult Ready() =>
        new(RoomReadyOutcome.Ready);

    public static RoomReadyResult NotFound() =>
        new(RoomReadyOutcome.RoomNotFound);

    public static RoomReadyResult Expired() =>
        new(RoomReadyOutcome.RoomExpired);

    public static RoomReadyResult NotHost() =>
        new(RoomReadyOutcome.NotHost);
}

public enum RoomReadyOutcome
{
    Ready,
    RoomNotFound,
    RoomExpired,
    NotHost
}
