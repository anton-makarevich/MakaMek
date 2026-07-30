namespace Sanet.MakaMek.Core.Services.Transport.Relay.Contracts;

/// <summary>
/// Wire DTO returned by <c>POST api/rooms/{code}/join</c>.
/// </summary>
public sealed record JoinResponse(
    bool Success,
    string? Role,
    Guid? PlayerId,
    Guid? HostId,
    string? SessionToken,
    HubError? Error);
