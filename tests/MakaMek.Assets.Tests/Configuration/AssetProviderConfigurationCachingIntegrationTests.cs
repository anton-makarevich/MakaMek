using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Assets.Configuration;
using Sanet.MakaMek.Assets.ResourceProviders;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Core.Data.Serialization.Converters;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Services;
using Shouldly;

namespace Sanet.MakaMek.Assets.Tests.Configuration;

public class AssetProviderConfigurationCachingIntegrationTests
{
    private sealed class FakeFileCachingService : IFileCachingService
    {
        private Dictionary<string, byte[]> Store { get; } = new();
        public Task<byte[]?> TryGetCachedFile(string cacheKey) =>
            Task.FromResult(Store.TryGetValue(cacheKey, out var bytes) ? (byte[]?)bytes : null);
        public Task SaveToCache(string cacheKey, byte[] content, string? version = null)
        {
            Store[cacheKey] = content;
            return Task.CompletedTask;
        }
        public Task ClearCache() { Store.Clear(); return Task.CompletedTask; }
        public Task<bool> IsCached(string cacheKey) => Task.FromResult(Store.ContainsKey(cacheKey));
        public Task RemoveFromCache(string cacheKey) { Store.Remove(cacheKey); return Task.CompletedTask; }
        public Task<string?> GetCacheVersion(string cacheKey) => Task.FromResult<string?>(null);
    }

    private static readonly AssetProviderConfigData[] Defaults =
    [
        new("bucket", ProviderType.Bucket, AssetType.Units, "https://data.makamek.nl/units/manifest.json", IsActive: true, IsDefault: true, SortOrder: 0)
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters =
        {
            new EnumConverter<MakaMekComponent>(),
            new EnumConverter<PartLocation>(),
            new EnumConverter<MovementType>(),
            new EnumConverter<UnitStatus>(),
            new EnumConverter<WeightClass>()
        }
    };

    private static void CreateTestMmuxPackage(string folder, string model)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{model}.mmux");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);

        var unitData = new UnitData
        {
            Model = model,
            Chassis = "Test",
            Mass = 20,
            EngineRating = 160,
            EngineType = "Standard",
            ArmorValues = new Dictionary<PartLocation, ArmorLocation>(),
            Equipment = new List<ComponentData>(),
            AdditionalAttributes = new Dictionary<string, string>(),
            Quirks = new Dictionary<string, string>()
        };

        var entry = archive.CreateEntry("unit.json");
        using (var entryStream = entry.Open())
        using (var writer = new StreamWriter(entryStream))
        {
            writer.Write(JsonSerializer.Serialize(unitData, JsonOptions));
        }

        var imageEntry = archive.CreateEntry("unit.png");
        using (var imageStream = imageEntry.Open())
        {
            var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            imageStream.Write(pngHeader, 0, pngHeader.Length);
        }
    }

    private static ILogger<AssetProviderConfigurationProvider> ConfigLogger() =>
        Substitute.For<ILogger<AssetProviderConfigurationProvider>>();

    private static ILoggerFactory CreateLoggerFactory() =>
        Substitute.For<ILoggerFactory>();

    [Fact]
    public async Task ActiveFilesystemProvider_IsResolvedLazily_AndLoadsUnits()
    {
        using var tempDir = new TempDir();
        CreateTestMmuxPackage(tempDir.Path, "INT-1A");

        var configStore = new FakeFileCachingService();
        var configProvider = new AssetProviderConfigurationProvider(Defaults, configStore, ConfigLogger());
        await configProvider.AddProvider(new AssetProviderConfigData(
            "local", ProviderType.Filesystem, AssetType.Units, tempDir.Path, IsActive: true, IsDefault: false, SortOrder: 5));

        var providerFactory = new ResourceStreamProviderFactory(configStore, CreateLoggerFactory());

        // Mimic the DI wiring in CoreServices: caching service gets a lazy factory that resolves
        // active providers from config on first access.
        var lazyTriggered = false;
        var cachingService = new UnitCachingService(
            [],
            CreateLoggerFactory(),
            async () =>
            {
                lazyTriggered = true;
                var configs = await configProvider.GetActiveProviders(AssetType.Units);
                return providerFactory.CreateAll(configs);
            });

        var models = await cachingService.GetAvailableModels();

        lazyTriggered.ShouldBeTrue();
        models.ShouldContain("INT-1A");
        UnitData? unitDataOption = await cachingService.GetUnitData("INT-1A");
        unitDataOption.HasValue.ShouldBeTrue();
        unitDataOption.Value.Model.ShouldBe("INT-1A");
    }

    [Fact]
    public async Task DeactivatingProvider_RemovesItsSources_OnReload()
    {
        using var tempDir = new TempDir();
        CreateTestMmuxPackage(tempDir.Path, "INT-2B");

        var configStore = new FakeFileCachingService();
        var configProvider = new AssetProviderConfigurationProvider(Defaults, configStore, ConfigLogger());
        await configProvider.AddProvider(new AssetProviderConfigData(
            "local", ProviderType.Filesystem, AssetType.Units, tempDir.Path, IsActive: true, IsDefault: false, SortOrder: 5));

        var providerFactory = new ResourceStreamProviderFactory(configStore, CreateLoggerFactory());

        var cachingService = new UnitCachingService(
            [],
            CreateLoggerFactory(),
            () => Resolve(configProvider, providerFactory, AssetType.Units));

        // Loaded from the active (local filesystem) provider.
        (await cachingService.GetAvailableModels()).ShouldContain("INT-2B");

        // Deactivate the local provider, then reload providers from current config exactly
        // as AssetLoadingViewModel.ReloadAsync does after a Settings change.
        await configProvider.SetProviderActive("local", false);
        var activeProperties = await configProvider.GetActiveProviders(AssetType.Units);
        await cachingService.SetProviders(providerFactory.CreateAll(activeProperties));
        await cachingService.ClearCache();

        // The provider list no longer includes the local folder, so its sources are gone.
        var models = await cachingService.GetAvailableModels();
        models.ShouldNotContain("INT-2B");
    }

    private static async Task<IReadOnlyList<IResourceStreamProvider>> Resolve(
        IAssetProviderConfigurationProvider configProvider,
        IResourceStreamProviderFactory providerFactory,
        AssetType assetType)
    {
        var configs = await configProvider.GetActiveProviders(assetType);
        return providerFactory.CreateAll(configs);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "makamek_it_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // best effort cleanup
            }
        }
    }
}
