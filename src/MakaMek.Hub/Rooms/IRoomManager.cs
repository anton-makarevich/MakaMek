namespace Sanet.MakaMek.Hub.Rooms;

/// <summary>
/// Manages the in-memory lifecycle of relay rooms.
/// Membership identities are Hub-minted device sessions; the Hub never deals in player identity.
/// </summary>
public interface IRoomManager
{
    RoomCreationResult CreateRoom(Guid hostGameId);
    RoomJoinResult JoinRoom(string roomCode, string? sessionToken);
    RoomReadyResult MarkRoomReady(string roomCode, string sessionToken);
    RoomCloseResult CloseRoom(string roomCode, string sessionToken);
    RoomRemoveMemberResult RemoveMember(string roomCode, string sessionToken, Guid targetDeviceSessionId);
    string? RegisterConnection(string roomCode, Guid deviceSessionId, string connectionId);
    bool UnregisterConnection(string roomCode, Guid deviceSessionId, string connectionId);
    string? GetHostConnectionId(string roomCode);
    string? GetConnectionId(string roomCode, Guid deviceSessionId);
    bool TryMarkHostDisconnected(string roomCode, Guid deviceSessionId, string connectionId);
    void MarkRoomForDissolution(string roomCode);
    void CancelRoomDissolution(string roomCode);

    /// <summary>
    /// Validates a session token for any role and returns the bound session when usable for relay.
    /// Returns null for missing, unknown, expired, revoked, dissolved, or room-mismatched tokens.
    /// </summary>
    RoomSession? AuthenticateSession(string sessionToken);
}
