namespace Sanet.MakaMek.Hub.Contracts;

/// <summary>
/// Join request for an existing relay room. A new device joins without a session token;
/// a rejoin presents the session token issued to its existing device session.
/// </summary>
public sealed record JoinRequest(string? SessionToken);
