using Sanet.MakaMek.Assets.Configuration;

namespace Sanet.MakaMek.Assets.ResourceProviders;

/// <summary>
/// Creates the appropriate <see cref="IResourceStreamProvider"/> for a given
/// <see cref="AssetProviderConfigData"/>.
/// </summary>
public interface IAssetProviderFactory
{
    /// <summary>
    /// Creates a single resource stream provider from an asset provider configuration.
    /// </summary>
    IResourceStreamProvider Create(AssetProviderConfigData config);

    /// <summary>
    /// Creates a resource stream provider for each provided configuration, preserving order.
    /// </summary>
    IReadOnlyList<IResourceStreamProvider> CreateAll(IEnumerable<AssetProviderConfigData> configs);
}
