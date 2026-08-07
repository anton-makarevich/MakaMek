namespace Sanet.MakaMek.Core.Services.Transport.Relay.Contracts;

/// <summary>
/// Wire enum mirroring the Hub REST error codes. Serialized as string names via
/// <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/>.
/// </summary>
public enum HubErrorCode
{
    HubAtCapacity,
    RoomNotFound,
    RoomExpired,
    HostNotReady,
    NotHost,
    RateLimited,
    MessageTooLarge,
    RoomFull,
    InvalidRoomState,
    MemberNotFound,
    CannotRemoveHost,
    HostDisconnected,
    ConnectionSuperseded
}
