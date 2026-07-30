namespace Sanet.MakaMek.Core.Services.Transport.Relay.Contracts;

/// <summary>
/// Wire DTO for <c>POST api/rooms</c>.
/// </summary>
public sealed record CreateRoomRequest(string PlayerName, Guid PlayerId);
