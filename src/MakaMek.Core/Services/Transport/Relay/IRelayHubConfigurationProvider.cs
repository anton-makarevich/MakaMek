namespace Sanet.MakaMek.Core.Services.Transport.Relay;

/// <summary>
/// Runtime source of truth for the relay hub used by online hosting and joining.
/// Seeds a built-in Demo hub from <see cref="RelayClientOptions"/> and persists
/// user-defined hubs together with the active selection.
/// </summary>
public interface IRelayHubConfigurationProvider
{
    /// <summary>
    /// Identifier of the currently selected hub.
    /// </summary>
    string ActiveHubId { get; }

    /// <summary>
    /// Base URL of the currently selected hub.
    /// </summary>
    string ActiveBaseUrl { get; }

    /// <summary>
    /// API key of the currently selected hub.
    /// </summary>
    string ActiveApiKey { get; }

    /// <summary>
    /// All known hubs, including the built-in Demo hub.
    /// </summary>
    IReadOnlyList<HubConfigData> Hubs { get; }

    /// <summary>
    /// Waits until any persisted hub configuration has been loaded.
    /// </summary>
    Task EnsureLoadedAsync();

    /// <summary>
    /// Adds a user-defined hub. The Demo hub is always seeded separately.
    /// </summary>
    Task AddHubAsync(HubConfigData hub);

    /// <summary>
    /// Updates an existing user-defined hub. Built-in hubs cannot be edited.
    /// </summary>
    /// <exception cref="InvalidOperationException">When <paramref name="id"/> refers to a built-in hub.</exception>
    Task UpdateHubAsync(string id, string name, string baseUrl, string apiKey);

    /// <summary>
    /// Removes a user-defined hub. Built-in hubs cannot be removed. If the removed
    /// hub was active, the Demo hub becomes active.
    /// </summary>
    /// <exception cref="InvalidOperationException">When <paramref name="id"/> refers to a built-in hub.</exception>
    Task RemoveHubAsync(string id);

    /// <summary>
    /// Selects the active hub.
    /// </summary>
    /// <exception cref="ArgumentException">When <paramref name="id"/> is unknown.</exception>
    Task SelectHubAsync(string id);
}
