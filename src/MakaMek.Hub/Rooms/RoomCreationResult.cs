namespace Sanet.MakaMek.Hub.Rooms;

/// <summary>
/// Internal room-creation outcome used to translate domain state into the REST contract.
/// </summary>
public sealed record RoomCreationResult(
    RoomCreationOutcome Outcome,
    Room? Room,
    RoomSession? Session,
    int ActiveRoomCount)
{
    public static RoomCreationResult Created(Room room, RoomSession session, int activeRoomCount) =>
        new(RoomCreationOutcome.Created, room, session, activeRoomCount);

    public static RoomCreationResult AtCapacity(int activeRoomCount) =>
        new(RoomCreationOutcome.HubAtCapacity, null, null, activeRoomCount);
}

public enum RoomCreationOutcome
{
    Created,
    HubAtCapacity
}
