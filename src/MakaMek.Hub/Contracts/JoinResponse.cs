namespace Sanet.MakaMek.Hub.Contracts;

public sealed record JoinResponse(
    bool Success,
    string? Role,
    Guid? PlayerId,
    Guid? HostId,
    string? SessionToken,
    HubError? Error);
