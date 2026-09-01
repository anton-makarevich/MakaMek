namespace Sanet.MakaMek.Assets.Configuration;

/// <summary>
/// Owns the configuration of asset source providers (bucket, GitHub, filesystem),
/// persisted through <see cref="IFileCachingService"/> and seeded with built-in defaults.
/// </summary>
public interface IAssetProviderConfigurationProvider
{
    /// <summary>
    /// Gets all configured providers ordered by ascending <see cref="AssetProviderConfigData.SortOrder"/>.
    /// </summary>
    Task<IReadOnlyList<AssetProviderConfigData>> GetProviders();

    /// <summary>
    /// Gets a single provider by id, or null if it does not exist.
    /// </summary>
    Task<AssetProviderConfigData?> GetProvider(string id);

    /// <summary>
    /// Adds a new (non-default) provider.
    /// </summary>
    Task AddProvider(AssetProviderConfigData provider);

    /// <summary>
    /// Updates the mutable fields of an existing provider, preserving its Id and IsDefault.
    /// </summary>
    Task UpdateProvider(string id, AssetProviderConfigData updated);

    /// <summary>
    /// Removes a provider. Default providers cannot be removed.
    /// </summary>
    Task RemoveProvider(string id);

    /// <summary>
    /// Activates or deactivates a provider. At least one provider must remain active per asset type.
    /// </summary>
    Task SetProviderActive(string id, bool isActive);

    /// <summary>
    /// Gets the active providers serving the given asset type, ordered by ascending SortOrder.
    /// </summary>
    Task<IReadOnlyList<AssetProviderConfigData>> GetActiveProviders(AssetType assetType);
}
