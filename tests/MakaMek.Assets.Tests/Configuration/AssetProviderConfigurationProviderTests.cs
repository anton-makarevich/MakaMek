using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Assets.Configuration;
using Sanet.MakaMek.Services;
using Shouldly;

namespace Sanet.MakaMek.Assets.Tests.Configuration;

public class AssetProviderConfigurationProviderTests
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
    public async Task GetProviders_WhenNoCache_SeedsDefaults_OrderedBySortOrder()
    {
        var sut = new AssetProviderConfigurationProvider(Defaults, new FakeFileCachingService(), Logger());

        var providers = await sut.GetProviders();

        providers.Count.ShouldBe(Defaults.Length);
        providers.Select(p => p.Id).ShouldBe(new[] { "bucket", "github" }, ignoreOrder: false);
        providers.ShouldAllBe(p => p.IsDefault);
    }

    [Fact]
    public async Task GetProvider_WhenExisting_ReturnsProvider()
    {
        var sut = new AssetProviderConfigurationProvider(Defaults, new FakeFileCachingService(), Logger());

        var provider = await sut.GetProvider("bucket");

        provider.ShouldNotBeNull();
        provider.Id.ShouldBe("bucket");
    }

    [Fact]
    public async Task GetProvider_WhenMissing_ReturnsNull()
    {
        var sut = new AssetProviderConfigurationProvider(Defaults, new FakeFileCachingService(), Logger());

        var provider = await sut.GetProvider("missing");

        provider.ShouldBeNull();
    }

    [Fact]
    public async Task AddProvider_AddsNonDefaultProvider()
    {
        var sut = new AssetProviderConfigurationProvider(Defaults, new FakeFileCachingService(), Logger());

        await sut.AddProvider(new AssetProviderConfigData(
            "local", ProviderType.Filesystem, AssetType.Hexes, "C:\\assets", IsActive: true, IsDefault: true, SortOrder: 5));

        var provider = await sut.GetProvider("local");
        provider.ShouldNotBeNull();
        provider.IsDefault.ShouldBeFalse();
    }

    [Fact]
    public async Task AddProvider_WhenDuplicateId_Throws()
    {
        var sut = new AssetProviderConfigurationProvider(Defaults, new FakeFileCachingService(), Logger());

        await Should.ThrowAsync<ArgumentException>(() => sut.AddProvider(Defaults[0]));
    }

    [Fact]
    public async Task UpdateProvider_PreservesIdAndIsDefault()
    {
        var sut = new AssetProviderConfigurationProvider(Defaults, new FakeFileCachingService(), Logger());

        await sut.UpdateProvider("bucket", new AssetProviderConfigData(
            "different", ProviderType.GitHub, AssetType.Hexes, "https://new", IsActive: false, IsDefault: false, SortOrder: 9));

        var provider = await sut.GetProvider("bucket");
        provider.ShouldNotBeNull();
        provider.Id.ShouldBe("bucket");
        provider.IsDefault.ShouldBeTrue();
        provider.UrlOrPath.ShouldBe("https://new");
        provider.ProviderType.ShouldBe(ProviderType.GitHub);
        provider.AssetType.ShouldBe(AssetType.Hexes);
        provider.IsActive.ShouldBeFalse();
        provider.SortOrder.ShouldBe(9);
    }

    [Fact]
    public async Task UpdateProvider_WhenOnlyActiveForAssetType_Throws()
    {
        var defaults = new[]
        {
            new AssetProviderConfigData("a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: true, IsDefault: true, SortOrder: 0)
        };
        var sut = new AssetProviderConfigurationProvider(defaults, new FakeFileCachingService(), Logger());

        await Should.ThrowAsync<InvalidOperationException>(() => sut.UpdateProvider("a", new AssetProviderConfigData(
            "a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: false, IsDefault: true, SortOrder: 0)));
    }

    [Fact]
    public async Task UpdateProvider_WhenMovingOnlyActiveProviderToNewAssetType_Throws()
    {
        var defaults = new[]
        {
            new AssetProviderConfigData("a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: true, IsDefault: true, SortOrder: 0)
        };
        var sut = new AssetProviderConfigurationProvider(defaults, new FakeFileCachingService(), Logger());

        await Should.ThrowAsync<InvalidOperationException>(() => sut.UpdateProvider("a", new AssetProviderConfigData(
            "a", ProviderType.Bucket, AssetType.Hexes, "h1", IsActive: true, IsDefault: true, SortOrder: 0)));
    }

    [Fact]
    public async Task UpdateProvider_WhenAnotherActiveProviderRemains_Succeeds()
    {
        var defaults = new[]
        {
            new AssetProviderConfigData("a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: true, IsDefault: true, SortOrder: 0),
            new AssetProviderConfigData("b", ProviderType.Bucket, AssetType.Units, "u2", IsActive: true, IsDefault: true, SortOrder: 1)
        };
        var sut = new AssetProviderConfigurationProvider(defaults, new FakeFileCachingService(), Logger());

        await sut.UpdateProvider("a", new AssetProviderConfigData(
            "a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: false, IsDefault: true, SortOrder: 0));

        var a = await sut.GetProvider("a");
        a.ShouldNotBeNull();
        a.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateProvider_WhenMissing_Throws()
    {
        var sut = new AssetProviderConfigurationProvider(Defaults, new FakeFileCachingService(), Logger());

        await Should.ThrowAsync<ArgumentException>(() => sut.UpdateProvider("missing", Defaults[0]));
    }

    [Fact]
    public async Task RemoveProvider_WhenAdded_RemovesIt()
    {
        var sut = new AssetProviderConfigurationProvider(Defaults, new FakeFileCachingService(), Logger());
        await sut.AddProvider(new AssetProviderConfigData(
            "local", ProviderType.Filesystem, AssetType.Hexes, "C:\\assets", IsActive: true, IsDefault: false, SortOrder: 5));
        await sut.AddProvider(new AssetProviderConfigData(
            "local2", ProviderType.Filesystem, AssetType.Hexes, "C:\\assets2", IsActive: true, IsDefault: false, SortOrder: 6));

        await sut.RemoveProvider("local");

        (await sut.GetProvider("local")).ShouldBeNull();
    }

    [Fact]
    public async Task RemoveProvider_WhenDefault_Throws()
    {
        var sut = new AssetProviderConfigurationProvider(Defaults, new FakeFileCachingService(), Logger());

        await Should.ThrowAsync<InvalidOperationException>(() => sut.RemoveProvider("bucket"));
    }

    [Fact]
    public async Task RemoveProvider_WhenOnlyActiveForAssetType_Throws()
    {
        var defaults = new[]
        {
            new AssetProviderConfigData("a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: true, IsDefault: true, SortOrder: 0)
        };
        var sut = new AssetProviderConfigurationProvider(defaults, new FakeFileCachingService(), Logger());

        await Should.ThrowAsync<InvalidOperationException>(() => sut.RemoveProvider("a"));
    }

    [Fact]
    public async Task SetProviderActive_Deactivates()
    {
        var defaults = new[]
        {
            new AssetProviderConfigData("a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: true, IsDefault: true, SortOrder: 0),
            new AssetProviderConfigData("b", ProviderType.Bucket, AssetType.Units, "u2", IsActive: true, IsDefault: true, SortOrder: 1)
        };
        var sut = new AssetProviderConfigurationProvider(defaults, new FakeFileCachingService(), Logger());

        await sut.SetProviderActive("a", false);

        var a = await sut.GetProvider("a");
        var b = await sut.GetProvider("b");
        a.ShouldNotBeNull();
        b.ShouldNotBeNull();
        a.IsActive.ShouldBeFalse();
        b.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task SetProviderActive_WhenOnlyActiveForAssetType_Throws()
    {
        var defaults = new[]
        {
            new AssetProviderConfigData("a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: true, IsDefault: true, SortOrder: 0)
        };
        var sut = new AssetProviderConfigurationProvider(defaults, new FakeFileCachingService(), Logger());

        await Should.ThrowAsync<InvalidOperationException>(() => sut.SetProviderActive("a", false));
    }

    [Fact]
    public async Task GetActiveProviders_ReturnsOnlyActiveForAssetType_OrderedBySortOrder()
    {
        var defaults = new[]
        {
            new AssetProviderConfigData("u1", ProviderType.Bucket, AssetType.Units, "a", IsActive: true, IsDefault: true, SortOrder: 2),
            new AssetProviderConfigData("u2", ProviderType.Bucket, AssetType.Units, "b", IsActive: false, IsDefault: true, SortOrder: 1),
            new AssetProviderConfigData("u3", ProviderType.Bucket, AssetType.Units, "c", IsActive: true, IsDefault: true, SortOrder: 0),
            new AssetProviderConfigData("h1", ProviderType.Bucket, AssetType.Hexes, "d", IsActive: true, IsDefault: true, SortOrder: 0)
        };
        var sut = new AssetProviderConfigurationProvider(defaults, new FakeFileCachingService(), Logger());

        var providers = await sut.GetActiveProviders(AssetType.Units);

        providers.Select(p => p.Id).ShouldBe(new[] { "u3", "u1" }, ignoreOrder: false);
    }

    [Theory]
    [InlineData(null!)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task AddProvider_WhenIdIsNullOrWhiteSpace_Throws(string? id)
    {
        var sut = new AssetProviderConfigurationProvider(Defaults, new FakeFileCachingService(), Logger());
        var provider = new AssetProviderConfigData(
            id!, ProviderType.Filesystem, AssetType.Hexes, "C:\\assets", IsActive: true, IsDefault: false, SortOrder: 0);

        await Should.ThrowAsync<ArgumentException>(() => sut.AddProvider(provider));
    }

    [Fact]
    public async Task RemoveProvider_WhenNotFound_ReturnsSilently()
    {
        var sut = new AssetProviderConfigurationProvider(Defaults, new FakeFileCachingService(), Logger());

        await sut.RemoveProvider("nonexistent");

        (await sut.GetProvider("nonexistent")).ShouldBeNull();
        (await sut.GetProviders()).Count.ShouldBe(Defaults.Length);
    }

    [Fact]
    public async Task RemoveProvider_WhenOnlyActiveNonDefaultForAssetType_Throws()
    {
        var defaults = new[]
        {
            new AssetProviderConfigData("a", ProviderType.Bucket, AssetType.Units, "u1", IsActive: true, IsDefault: true, SortOrder: 0),
            new AssetProviderConfigData("b", ProviderType.Filesystem, AssetType.Units, "u2", IsActive: true, IsDefault: false, SortOrder: 1)
        };
        var sut = new AssetProviderConfigurationProvider(defaults, new FakeFileCachingService(), Logger());

        await Should.ThrowAsync<InvalidOperationException>(() => sut.RemoveProvider("b"));
    }

    [Fact]
    public async Task SetProviderActive_WhenNotFound_Throws()
    {
        var sut = new AssetProviderConfigurationProvider(Defaults, new FakeFileCachingService(), Logger());

        await Should.ThrowAsync<ArgumentException>(() => sut.SetProviderActive("nonexistent", false));
    }

    [Fact]
    public async Task SetProviderActive_WhenSameValue_ReturnsSilently()
    {
        var sut = new AssetProviderConfigurationProvider(Defaults, new FakeFileCachingService(), Logger());

        await sut.SetProviderActive("bucket", true);

        var provider = await sut.GetProvider("bucket");
        provider.ShouldNotBeNull();
        provider.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task AddProvider_WhenPersistFails_RevertsAddition()
    {
        var cachingService = new FailingSaveCachingService { FailNextSave = true };
        var sut = new AssetProviderConfigurationProvider(Defaults, cachingService, Logger());

        await Should.ThrowAsync<InvalidOperationException>(() => sut.AddProvider(new AssetProviderConfigData(
            "local", ProviderType.Filesystem, AssetType.Hexes, "C:\\assets", IsActive: true, IsDefault: false, SortOrder: 5)));

        (await sut.GetProvider("local")).ShouldBeNull();
        (await sut.GetProviders()).Count.ShouldBe(Defaults.Length);
    }

    [Fact]
    public async Task UpdateProvider_WhenPersistFails_RevertsUpdate()
    {
        var cachingService = new FailingSaveCachingService { FailNextSave = true };
        var sut = new AssetProviderConfigurationProvider(Defaults, cachingService, Logger());

        await Should.ThrowAsync<InvalidOperationException>(() => sut.UpdateProvider("bucket", new AssetProviderConfigData(
            "different", ProviderType.GitHub, AssetType.Hexes, "https://new", IsActive: false, IsDefault: false, SortOrder: 9)));

        var provider = await sut.GetProvider("bucket");
        provider.ShouldNotBeNull();
        provider.UrlOrPath.ShouldBe("https://data.makamek.nl/units/manifest.json");
    }

    [Fact]
    public async Task RemoveProvider_WhenPersistFails_RevertsRemoval()
    {
        var cachingService = new FailingSaveCachingService();
        var sut = new AssetProviderConfigurationProvider(Defaults, cachingService, Logger());
        await sut.AddProvider(new AssetProviderConfigData(
            "local", ProviderType.Filesystem, AssetType.Hexes, "C:\\assets", IsActive: true, IsDefault: false, SortOrder: 5));
        // Force a successful persist so the provider is added in memory
        // Then make persist fail on the next call (remove)
        cachingService.FailNextSave = true;

        await Should.ThrowAsync<InvalidOperationException>(() => sut.RemoveProvider("local"));

        (await sut.GetProvider("local")).ShouldNotBeNull();
    }

    [Fact]
    public async Task SetProviderActive_WhenPersistFails_RevertsActivation()
    {
        var cachingService = new FailingSaveCachingService { FailNextSave = true };
        var sut = new AssetProviderConfigurationProvider(Defaults, cachingService, Logger());

        await Should.ThrowAsync<InvalidOperationException>(() => sut.SetProviderActive("bucket", false));

        var provider = await sut.GetProvider("bucket");
        provider.ShouldNotBeNull();
        provider.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Load_WhenCacheThrows_SeedsDefaults()
    {
        var cachingService = new ThrowingLoadCachingService();
        var sut = new AssetProviderConfigurationProvider(Defaults, cachingService, Logger());

        await sut.EnsureLoadedAsync();

        var providers = await sut.GetProviders();
        providers.Count.ShouldBe(Defaults.Length);
        providers.ShouldAllBe(p => p.IsDefault);
    }

    private sealed class FailingSaveCachingService : IFileCachingService
    {
        public bool FailNextSave { get; set; }

        public Task<byte[]?> TryGetCachedFile(string cacheKey) =>
            Task.FromResult<byte[]?>(null);
        public Task SaveToCache(string cacheKey, byte[] content, string? version = null)
        {
            if (FailNextSave)
                throw new InvalidOperationException("Simulated save failure");
            return Task.CompletedTask;
        }
        public Task ClearCache() => Task.CompletedTask;
        public Task<bool> IsCached(string cacheKey) => Task.FromResult(false);
        public Task RemoveFromCache(string cacheKey) => Task.CompletedTask;
        public Task<string?> GetCacheVersion(string cacheKey) => Task.FromResult<string?>(null);
    }

    private sealed class ThrowingLoadCachingService : IFileCachingService
    {
        public Task<byte[]?> TryGetCachedFile(string cacheKey) =>
            throw new InvalidOperationException("Simulated load failure");
        public Task SaveToCache(string cacheKey, byte[] content, string? version = null) =>
            Task.CompletedTask;
        public Task ClearCache() => Task.CompletedTask;
        public Task<bool> IsCached(string cacheKey) => Task.FromResult(false);
        public Task RemoveFromCache(string cacheKey) => Task.CompletedTask;
        public Task<string?> GetCacheVersion(string cacheKey) => Task.FromResult<string?>(null);
    }
}
