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
}
