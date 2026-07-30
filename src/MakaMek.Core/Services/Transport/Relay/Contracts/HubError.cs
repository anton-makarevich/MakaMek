namespace Sanet.MakaMek.Core.Services.Transport.Relay.Contracts;

/// <summary>
/// Wire DTO mirroring the Hub REST error body.
/// </summary>
public sealed record HubError(
    HubErrorCode Code,
    string Message,
    int? ActiveRoomCount = null);
