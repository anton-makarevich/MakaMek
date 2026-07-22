namespace Sanet.MakaMek.Hub.Contracts;

public enum HubErrorCode
{
    HubAtCapacity,
    RoomNotFound,
    RoomExpired,
    HostNotReady,
    NotHost,
    RateLimited,
    HostPlayerIdConflict
}