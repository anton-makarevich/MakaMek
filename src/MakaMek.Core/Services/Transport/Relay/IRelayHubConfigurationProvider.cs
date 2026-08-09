namespace Sanet.MakaMek.Core.Services.Transport.Relay;

/// <summary>
/// Runtime source of truth for the relay hub used by online hosting and joining.
/// Seeds a built-in Demo hub from <see cref="RelayClientOptions"/> and persists
/// user-defined hubs together with the active selection.
/// </summary>
/// <remarks>
/// Every method and accessor waits for any persisted hub configuration to load
/// before returning, so consumers never need to manage the load themselves.
/// </remarks>
public interface IRelayHubConfigurationProvider
{
    /// <summary>
    /// Returns the connection options of the currently selected hub, waiting for
    /// any persisted hub configuration to load first. Never returns null; the base
    /// URL may be blank when no hub has been configured on this platform.
    /// </summary>
    Task<RelayClientOptions> GetActiveOptionsAsync();

    /// <summary>
    /// Returns the identifier of the currently selected hub, waiting for any
    /// persisted hub configuration to load first.
    /// </summary>
    Task<string> GetActiveHubIdAsync();

    /// <summary>
    /// Returns all known hubs, including the built-in Demo hub, waiting for any
    /// persisted hub configuration to load first.
    /// </summary>
    Task<IReadOnlyList<HubConfigData>> GetHubsAsync();

    /// <summary>
    /// Adds a user-defined hub. The Demo hub is always seeded separately.
    /// </summary>
    /// <exception cref="ArgumentException">When <paramref name="hub"/>.Id is blank or already in use.</exception>
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
