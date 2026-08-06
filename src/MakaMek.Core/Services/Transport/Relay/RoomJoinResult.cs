namespace Sanet.MakaMek.Core.Services.Transport.Relay;

/// <summary>
/// Result of joining a relay room. Success carries the values needed to set up the relay publisher.
/// </summary>
public sealed record RoomJoinResult(
    bool Success,
    string? RoomCode,
    string? SessionToken,
    string? Role,
    Guid? DeviceSessionId,
    Guid? HostGameId,
    RelayClientError? Error)
{
    public static RoomJoinResult Succeeded(
        string roomCode,
        string sessionToken,
        string role,
        Guid deviceSessionId,
        Guid hostGameId) =>
        new(true, roomCode, sessionToken, role, deviceSessionId, hostGameId, null);

    public static RoomJoinResult Failed(RelayClientError error) =>
        new(false, null, null, null, null, null, error);
}
