namespace Sanet.MakaMek.Hub.Rooms;

/// <summary>
/// Manages the in-memory lifecycle of relay rooms.
/// </summary>
public interface IRoomManager
{
    RoomCreationResult CreateRoom(string playerName, Guid playerId);
    RoomJoinResult JoinRoom(string roomCode, string playerName, Guid playerId);
    RoomReadyResult MarkRoomReady(string roomCode, string sessionToken);
    RoomCloseResult CloseRoom(string roomCode, string sessionToken);
    RoomRemoveMemberResult RemoveMember(string roomCode, string sessionToken, Guid targetPlayerId);
    string? RegisterConnection(string roomCode, Guid playerId, string connectionId);
    bool UnregisterConnection(string roomCode, Guid playerId, string connectionId);
    string? GetHostConnectionId(string roomCode);
    void MarkRoomForDissolution(string roomCode);
    void CancelRoomDissolution(string roomCode);

    /// <summary>
    /// Validates a session token for any role and returns the bound session when usable for relay.
    /// Returns null for missing, unknown, expired, revoked, dissolved, or room-mismatched tokens.
    /// </summary>
    RoomSession? AuthenticateSession(string sessionToken);
}
