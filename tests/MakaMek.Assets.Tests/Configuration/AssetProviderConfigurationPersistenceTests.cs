using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Assets.Configuration;
using Sanet.MakaMek.Services;
using Shouldly;

namespace Sanet.MakaMek.Assets.Tests.Configuration;

public class AssetProviderConfigurationPersistenceTests
{
    private static readonly AssetProviderConfigData[] Defaults =
    [
        new("bucket", ProviderType.Bucket, AssetType.Units, "https://data.makamek.nl/units/manifest.json", IsActive: true, IsDefault: true, SortOrder: 0),
        new("github", ProviderType.GitHub, AssetType.Units, "https://api.github.com/units", IsActive: true, IsDefault: true, SortOrder: 1)
    ];

    private sealed class FakeFileCachingService : IFileCachingService
    {
        public Dictionary<string, byte[]> Store { get; } = new();
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

    private static ILogger<AssetProviderConfigurationProvider> Logger() =>
        Substitute.For<ILogger<AssetProviderConfigurationProvider>>();

    [Fact]
    public async Task MutatedProviders_SurviveRecreation_FromPersistedCache()
    {
        var store = new FakeFileCachingService();
        var first = new AssetProviderConfigurationProvider(Defaults, store, Logger());

        await first.AddProvider(new AssetProviderConfigData(
            "local", ProviderType.Filesystem, AssetType.Hexes, "C:\\assets", IsActive: true, IsDefault: false, SortOrder: 5));
        await first.SetProviderActive("github", false);

        // A brand new provider instance over the same persisted store must recover state.
        var second = new AssetProviderConfigurationProvider(Defaults, store, Logger());

        var providers = await second.GetProviders();
        var local = await second.GetProvider("local");
        var github = await second.GetProvider("github");

        local.ShouldNotBeNull();
        local.IsDefault.ShouldBeFalse();

        github.ShouldNotBeNull();
        github.IsActive.ShouldBeFalse();
        github.IsDefault.ShouldBeTrue();

        providers.Count.ShouldBe(3);
    }

    [Fact]
    public async Task DeploymentOfADefault_SurvivesRecreation()
    {
        var store = new FakeFileCachingService();
        var defaults = new[]
        {
            new AssetProviderConfigData("a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: true, IsDefault: true, SortOrder: 0),
            new AssetProviderConfigData("b", ProviderType.Bucket, AssetType.Units, "u2", IsActive: true, IsDefault: true, SortOrder: 1)
        };

        var first = new AssetProviderConfigurationProvider(defaults, store, Logger());
        await first.SetProviderActive("a", false);

        var second = new AssetProviderConfigurationProvider(defaults, store, Logger());

        var a = await second.GetProvider("a");
        var b = await second.GetProvider("b");

        a.ShouldNotBeNull();
        b.ShouldNotBeNull();
        a.IsActive.ShouldBeFalse();
        a.IsDefault.ShouldBeTrue();
        b.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task DefaultsNotPresentInCache_AreMergedBack()
    {
        // A cache that only stores a stale, minimal subset still yields the full default set.
        var store = new FakeFileCachingService();

        // Simulate a persisted cache predating the 'github' default: only 'bucket' is stored.
        const string staleState = """
            {"Providers":[
                {"Id":"bucket","ProviderType":0,"AssetType":0,"UrlOrPath":"https://data.makamek.nl/units/manifest.json","IsActive":false,"IsDefault":true,"SortOrder":0}
            ]}
            """;
        store.Store["AssetProviders"] = System.Text.Encoding.UTF8.GetBytes(staleState);

        var sut = new AssetProviderConfigurationProvider(Defaults, store, Logger());
        var providers = await sut.GetProviders();

        providers.Select(p => p.Id).ShouldContain("bucket");
        providers.Select(p => p.Id).ShouldContain("github");
        var bucket = await sut.GetProvider("bucket");
        var github = await sut.GetProvider("github");
        bucket.ShouldNotBeNull();
        github.ShouldNotBeNull();
        bucket.IsActive.ShouldBeFalse();
        bucket.IsDefault.ShouldBeTrue();
        github.IsDefault.ShouldBeTrue();
    }
}
