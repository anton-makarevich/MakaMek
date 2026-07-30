namespace Sanet.MakaMek.Core.Services.Transport.Relay.Contracts;

/// <summary>
/// Wire DTO for <c>POST api/rooms/{code}/join</c>.
/// </summary>
public sealed record JoinRequest(string PlayerName, Guid PlayerId);
