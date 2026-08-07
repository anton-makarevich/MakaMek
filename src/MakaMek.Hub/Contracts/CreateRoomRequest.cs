namespace Sanet.MakaMek.Hub.Contracts;

/// <summary>
/// Identifies the host's ServerGame for which a relay room is created.
/// No player identity is accepted at the Hub boundary.
/// </summary>
public sealed record CreateRoomRequest(Guid GameId);
