namespace Sanet.MakaMek.Assets.Configuration;

/// <summary>
/// Configuration data describing a single asset source provider.
/// Analogous to <c>HubConfigData</c> for relay hubs.
/// </summary>
/// <param name="Id">Unique identifier of the provider.</param>
/// <param name="ProviderType">The type of provider (Bucket, GitHub or Filesystem).</param>
/// <param name="AssetType">The kind of assets the provider serves.</param>
/// <param name="UrlOrPath">Remote URL for Bucket/GitHub providers, or a local path for Filesystem providers.</param>
/// <param name="IsActive">Whether the provider is currently activated for loading assets.</param>
/// <param name="IsDefault">Whether this is a built-in provider (cannot be removed).</param>
/// <param name="SortOrder">Lower order = higher priority; when multiple providers serve the same asset, a lower SortOrder overwrites a higher one.</param>
public record AssetProviderConfigData(
    string Id,
    ProviderType ProviderType,
    AssetType AssetType,
    string UrlOrPath,
    bool IsActive,
    bool IsDefault,
    int SortOrder);
