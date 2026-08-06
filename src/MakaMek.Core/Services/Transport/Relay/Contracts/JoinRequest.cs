namespace Sanet.MakaMek.Core.Services.Transport.Relay.Contracts;

/// <summary>
/// Wire DTO for <c>POST api/rooms/{code}/join</c>. A new device joins without a
/// session token; a rejoin presents the token issued to its device session.
/// </summary>
public sealed record JoinRequest(string? SessionToken);
