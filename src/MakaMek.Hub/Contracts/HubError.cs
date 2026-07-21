namespace Sanet.MakaMek.Hub.Contracts;

/// <summary>
/// A public error returned by the relay's REST contract.
/// </summary>
public sealed record HubError(
    HubErrorCode Code,
    string Message,
    int? ActiveRoomCount = null);