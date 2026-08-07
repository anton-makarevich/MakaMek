namespace Sanet.MakaMek.Hub.Rooms;

public enum RoomJoinOutcome
{
    Joined,
    RoomNotFound,
    RoomExpired,
    HostNotReady,
    RoomFull,
    Forbidden
}