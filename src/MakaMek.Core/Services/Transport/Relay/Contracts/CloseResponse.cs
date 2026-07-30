namespace Sanet.MakaMek.Core.Services.Transport.Relay.Contracts;

/// <summary>
/// Wire DTO returned by <c>POST api/rooms/{code}/close</c>.
/// </summary>
public sealed record CloseResponse(
    bool Success,
    HubError? Error);
