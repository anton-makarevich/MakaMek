namespace Sanet.MakaMek.Hub.Rooms;

/// <summary>
/// Manages the in-memory lifecycle of relay rooms.
/// </summary>
public interface IRoomManager
{
    RoomCreationResult CreateRoom(string playerName, Guid playerId);
}
