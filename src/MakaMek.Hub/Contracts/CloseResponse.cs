namespace Sanet.MakaMek.Hub.Contracts;

public sealed record CloseResponse(
    bool Success,
    HubError? Error);
