namespace Sanet.MakaMek.Hub.Contracts;

public enum HubErrorCode
{
    HubAtCapacity,
    RoomNotFound,
    RoomExpired,
    HostNotReady,
    NotHost,
    RateLimited,
    MessageTooLarge,
    HostPlayerIdConflict,
    RoomFull,
    InvalidRoomState,
    MemberNotFound,
    CannotRemoveHost
}