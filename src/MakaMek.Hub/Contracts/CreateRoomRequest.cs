namespace Sanet.MakaMek.Hub.Contracts;

/// <summary>
/// Identifies the anonymous player creating a relay room.
/// </summary>
public sealed record CreateRoomRequest(string PlayerName, Guid PlayerId);
