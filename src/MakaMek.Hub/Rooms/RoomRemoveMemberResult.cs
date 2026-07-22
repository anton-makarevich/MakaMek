namespace Sanet.MakaMek.Hub.Rooms;

public sealed record RoomRemoveMemberResult(RoomRemoveMemberOutcome Outcome)
{
    public static RoomRemoveMemberResult Removed() =>
        new(RoomRemoveMemberOutcome.Removed);

    public static RoomRemoveMemberResult NotFound() =>
        new(RoomRemoveMemberOutcome.RoomNotFound);

    public static RoomRemoveMemberResult Expired() =>
        new(RoomRemoveMemberOutcome.RoomExpired);

    public static RoomRemoveMemberResult NotHost() =>
        new(RoomRemoveMemberOutcome.NotHost);

    public static RoomRemoveMemberResult MemberNotFound() =>
        new(RoomRemoveMemberOutcome.MemberNotFound);

    public static RoomRemoveMemberResult CannotRemoveHost() =>
        new(RoomRemoveMemberOutcome.CannotRemoveHost);
}

public enum RoomRemoveMemberOutcome
{
    Removed,
    RoomNotFound,
    RoomExpired,
    NotHost,
    MemberNotFound,
    CannotRemoveHost
}
