using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Configuration;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Assets.ResourceProviders;

/// <summary>
/// Maps <see cref="ProviderType"/> to the concrete <see cref="IResourceStreamProvider"/>
/// implementation and resolves the per-asset-type file extension and manifest path.
/// </summary>
public sealed class AssetProviderFactory : IAssetProviderFactory
{
    private const string UnitsExtension = "mmux";
    private const string HexesExtension = "mmtx";
    private const string UnitsManifest = "units/manifest.json";
    private const string HexesManifest = "hexes/manifest.json";

    private readonly IFileCachingService _cachingService;
    private readonly ILoggerFactory _loggerFactory;

    public AssetProviderFactory(IFileCachingService cachingService, ILoggerFactory loggerFactory)
    {
        _cachingService = cachingService ?? throw new ArgumentNullException(nameof(cachingService));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public IResourceStreamProvider Create(AssetProviderConfigData config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.ProviderType switch
        {
            ProviderType.Bucket => new BucketResourceStreamProvider(
                GetManifestPath(config.AssetType),
                GetFileExtension(config.AssetType),
                config.UrlOrPath,
                _cachingService,
                _loggerFactory.CreateLogger<BucketResourceStreamProvider>()),
            ProviderType.GitHub => new GitHubResourceStreamProvider(
                GetFileExtension(config.AssetType),
                config.UrlOrPath,
                _cachingService,
                _loggerFactory.CreateLogger<GitHubResourceStreamProvider>()),
            ProviderType.Filesystem => new LocalFolderResourceStreamProvider(
                config.UrlOrPath,
                GetFileExtension(config.AssetType)),
            _ => throw new ArgumentOutOfRangeException(nameof(config.ProviderType), config.ProviderType,
                $"Unsupported provider type '{config.ProviderType}'.")
        };
    }

    public IReadOnlyList<IResourceStreamProvider> CreateAll(IEnumerable<AssetProviderConfigData> configs)
    {
        ArgumentNullException.ThrowIfNull(configs);

        return configs.Select(Create).ToList();
    }

    private static string GetFileExtension(AssetType assetType)
    {
        return assetType switch
        {
            AssetType.Units => UnitsExtension,
            AssetType.Hexes => HexesExtension,
            _ => throw new ArgumentOutOfRangeException(nameof(assetType), assetType,
                $"Unsupported asset type '{assetType}'.")
        };
    }

    private static string GetManifestPath(AssetType assetType)
    {
        return assetType switch
        {
            AssetType.Units => UnitsManifest,
            AssetType.Hexes => HexesManifest,
            _ => throw new ArgumentOutOfRangeException(nameof(assetType), assetType,
                $"Unsupported asset type '{assetType}'.")
        };
    }
}
