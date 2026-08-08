namespace Sanet.MakaMek.Core.Services.Transport.Relay;

/// <summary>
/// Data shape for a single relay hub entry the player can connect to.
/// </summary>
/// <param name="Id">Stable identifier of the hub.</param>
/// <param name="Name">Display name shown in the UI.</param>
/// <param name="BaseUrl">Base URL of the MakaMek Hub REST room API.</param>
/// <param name="ApiKey">Shared API key sent as the <c>X-Api-Key</c> header.</param>
/// <param name="IsBuiltIn">Marks the built-in Demo hub; built-in hubs cannot be edited or removed.</param>
public sealed record HubConfigData(
    string Id,
    string Name,
    string BaseUrl,
    string ApiKey,
    bool IsBuiltIn);
