namespace Sanet.MakaMek.Hub.Contracts;

public sealed record ReadyResponse(
    bool Success,
    HubError? Error);
