namespace Sanet.MakaMek.Core.Services.Transport.Relay.Contracts;

/// <summary>
/// Wire DTO returned by <c>POST api/rooms/{code}/ready</c>.
/// </summary>
public sealed record ReadyResponse(
    bool Success,
    HubError? Error);
