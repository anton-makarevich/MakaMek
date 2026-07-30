namespace Sanet.MakaMek.Core.Services.Transport.Relay;

/// <summary>
/// Result of creating a relay room. Success carries the values needed to set up the relay publisher.
/// </summary>
public sealed record RoomCreateResult(
    bool Success,
    string? RoomCode,
    string? SessionToken,
    string? Role,
    Guid? PlayerId,
    Guid? HostId,
    RelayClientError? Error)
{
    public static RoomCreateResult Succeeded(
        string roomCode,
        string sessionToken,
        string role,
        Guid playerId,
        Guid hostId) =>
        new(true, roomCode, sessionToken, role, playerId, hostId, null);

    public static RoomCreateResult Failed(RelayClientError error) =>
        new(false, null, null, null, null, null, error);
}
