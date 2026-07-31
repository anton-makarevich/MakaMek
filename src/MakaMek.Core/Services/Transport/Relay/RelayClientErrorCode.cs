namespace Sanet.MakaMek.Core.Services.Transport.Relay;

/// <summary>
/// Stable client-facing error codes for Hub REST room operations.
/// Mirrors relevant <see cref="Contracts.HubErrorCode"/> values and adds transport/authorization cases.
/// </summary>
public enum RelayClientErrorCode
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
    CannotRemoveHost,
    HostDisconnected,
    ConnectionSuperseded,
    Unauthorized,
    ValidationError,
    Timeout,
    NetworkError,
    DeserializationError,
    ConfigurationError,
    Unknown
}
