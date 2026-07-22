namespace Sanet.MakaMek.Hub.Contracts;

public sealed record RemoveMemberResponse(
    bool Success,
    HubError? Error);
