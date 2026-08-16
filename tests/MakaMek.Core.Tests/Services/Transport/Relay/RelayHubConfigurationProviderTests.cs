using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sanet.MakaMek.Core.Services.Transport.Relay;
using Sanet.Transport.SignalR.Client.Relay;
using Sanet.MakaMek.Services;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services.Transport.Relay;

public class RelayHubConfigurationProviderTests
{
    private const string CacheKey = "HubConfigurations";
    private readonly IFileCachingService _cachingService = Substitute.For<IFileCachingService>();
    private readonly ILogger<RelayHubConfigurationProvider> _logger = Substitute.For<ILogger<RelayHubConfigurationProvider>>();

    private IOptions<RelayClientOptions> CreateOptions(string baseUrl = "http://demo.local", string apiKey = "demo-key") =>
        Options.Create(new RelayClientOptions { BaseUrl = baseUrl, ApiKey = apiKey });

    private RelayHubConfigurationProvider CreateSut(
        string baseUrl = "http://demo.local",
        string apiKey = "demo-key")
    {
        return new RelayHubConfigurationProvider(CreateOptions(baseUrl, apiKey), _cachingService, _logger);
    }

    private static byte[] SerializeState(List<HubConfigData> userHubs, string? activeHubId)
    {
        var json = JsonSerializer.Serialize(new
        {
            Hubs = userHubs,
            ActiveHubId = activeHubId
        });
        return Encoding.UTF8.GetBytes(json);
    }

    // ---------- Demo hub seeding ----------

    [Fact]
    public async Task Constructor_SeedsDemoHubFromOptions()
    {
        var sut = CreateSut(baseUrl: "http://env-hub.example", apiKey: "env-key");

        await sut.EnsureLoadedAsync();

        sut.Hubs.ShouldHaveSingleItem();
        var demo = sut.Hubs.Single();
        demo.IsBuiltIn.ShouldBeTrue();
        demo.BaseUrl.ShouldBe("http://env-hub.example");
        demo.ApiKey.ShouldBe("env-key");
        sut.ActiveHubId.ShouldBe(demo.Id);
        sut.ActiveBaseUrl.ShouldBe("http://env-hub.example");
        sut.ActiveApiKey.ShouldBe("env-key");
    }

    [Fact]
    public async Task GetActiveOptionsAsync_ReturnsActiveHubBaseUrlAndApiKey()
    {
        var sut = CreateSut(baseUrl: "http://env-hub.example", apiKey: "env-key");

        var options = await sut.GetActiveOptions();

        options.ShouldNotBeNull();
        options.BaseUrl.ShouldBe("http://env-hub.example");
        options.ApiKey.ShouldBe("env-key");
    }

    [Fact]
    public async Task GetActiveOptionsAsync_ReflectsLoadedPersistenceAndSelection()
    {
        _cachingService.TryGetCachedFile(CacheKey).Returns(
            SerializeState(
                [new HubConfigData("custom-1", "My Hub", "http://my-hub.example", "my-key", IsBuiltIn: false)],
                "custom-1"));

        var sut = CreateSut();

        var options = await sut.GetActiveOptions();

        options.ShouldNotBeNull();
        options.BaseUrl.ShouldBe("http://my-hub.example");
        options.ApiKey.ShouldBe("my-key");
    }

    [Fact]
    public async Task AddHub_AddsUserHub_ButActiveConfigurationStaysOnDemo()
    {
        var sut = CreateSut();

        await sut.AddHub(new HubConfigData("custom-1", "My Hub", "http://my-hub.example", "my-key", IsBuiltIn: false));

        sut.Hubs.Count.ShouldBe(2);
        var added = sut.Hubs.Single(h => h.Id == "custom-1");
        added.IsBuiltIn.ShouldBeFalse();
        added.Name.ShouldBe("My Hub");
        sut.ActiveHubId.ShouldBe(sut.Hubs.Single(h => h.IsBuiltIn).Id);
        sut.ActiveBaseUrl.ShouldBe("http://demo.local");
        sut.ActiveApiKey.ShouldBe("demo-key");
    }

    [Fact]
    public async Task AddHub_PersistsThroughCachingService()
    {
        var sut = CreateSut();

        await sut.AddHub(new HubConfigData("custom-1", "My Hub", "http://my-hub.example", "my-key", IsBuiltIn: false));

        await _cachingService.Received(1).SaveToCache(CacheKey, Arg.Any<byte[]>());
    }

    [Fact]
    public async Task AddHub_WithReservedDemoId_ThrowsAndKeepsDemoHubUnchanged()
    {
        var sut = CreateSut();
        var demoId = sut.Hubs.Single(h => h.IsBuiltIn).Id;

        await Should.ThrowAsync<ArgumentException>(
            () => sut.AddHub(new HubConfigData(demoId, "Hacked", "http://evil.example", "evil-key", IsBuiltIn: false)));

        var demo = sut.Hubs.Single(h => h.IsBuiltIn);
        demo.Id.ShouldBe(demoId);
        demo.BaseUrl.ShouldBe("http://demo.local");
        demo.Name.ShouldBe("Demo Hub");
        demo.IsBuiltIn.ShouldBeTrue();
        await _cachingService.DidNotReceive().SaveToCache(CacheKey, Arg.Any<byte[]>());
    }

    [Fact]
    public async Task AddHub_WithBlankId_Throws()
    {
        var sut = CreateSut();

        await Should.ThrowAsync<ArgumentException>(
            () => sut.AddHub(new HubConfigData("", "My Hub", "http://my-hub.example", "my-key", IsBuiltIn: false)));

        sut.Hubs.ShouldHaveSingleItem();
        await _cachingService.DidNotReceive().SaveToCache(CacheKey, Arg.Any<byte[]>());
    }

    [Fact]
    public async Task AddHub_WithDuplicateUserId_ThrowsAndKeepsExistingEntry()
    {
        var sut = CreateSut();
        await sut.AddHub(new HubConfigData("custom-1", "My Hub", "http://my-hub.example", "my-key", IsBuiltIn: false));

        await Should.ThrowAsync<ArgumentException>(
            () => sut.AddHub(new HubConfigData("custom-1", "Other Hub", "http://other.example", "other-key", IsBuiltIn: false)));

        var existing = sut.Hubs.Single(h => h.Id == "custom-1");
        existing.Name.ShouldBe("My Hub");
        existing.BaseUrl.ShouldBe("http://my-hub.example");
        existing.ApiKey.ShouldBe("my-key");
    }

    // ---------- Select ----------

    [Fact]
    public async Task SelectHub_ActivatesUserHubConfiguration()
    {
        var sut = CreateSut();
        await sut.AddHub(new HubConfigData("custom-1", "My Hub", "http://my-hub.example", "my-key", IsBuiltIn: false));

        await sut.SelectHub("custom-1");

        sut.ActiveHubId.ShouldBe("custom-1");
        sut.ActiveBaseUrl.ShouldBe("http://my-hub.example");
        sut.ActiveApiKey.ShouldBe("my-key");
        await _cachingService.Received().SaveToCache(CacheKey, Arg.Any<byte[]>());
    }

    [Fact]
    public async Task SelectHub_WithUnknownId_Throws()
    {
        var sut = CreateSut();

        await Should.ThrowAsync<ArgumentException>(() => sut.SelectHub("unknown"));
    }

    [Fact]
    public async Task SelectHub_DemoHubIsSelectable()
    {
        var sut = CreateSut();
        await sut.AddHub(new HubConfigData("custom-1", "My Hub", "http://my-hub.example", "my-key", IsBuiltIn: false));
        var demoId = sut.Hubs.Single(h => h.IsBuiltIn).Id;

        await sut.SelectHub("custom-1");
        await sut.SelectHub(demoId);

        sut.ActiveHubId.ShouldBe(demoId);
        sut.ActiveBaseUrl.ShouldBe("http://demo.local");
    }

    // ---------- Update ----------

    [Fact]
    public async Task UpdateHub_UpdatesUserHubValues()
    {
        var sut = CreateSut();
        await sut.AddHub(new HubConfigData("custom-1", "My Hub", "http://my-hub.example", "my-key", IsBuiltIn: false));

        await sut.UpdateHub("custom-1", "Renamed Hub", "http://renamed.example", "renamed-key");

        var updated = sut.Hubs.Single(h => h.Id == "custom-1");
        updated.Name.ShouldBe("Renamed Hub");
        updated.BaseUrl.ShouldBe("http://renamed.example");
        updated.ApiKey.ShouldBe("renamed-key");
        updated.IsBuiltIn.ShouldBeFalse();
        await _cachingService.Received().SaveToCache(CacheKey, Arg.Any<byte[]>());
    }

    [Fact]
    public async Task UpdateHub_WithUnknownId_Throws()
    {
        var sut = CreateSut();

        await Should.ThrowAsync<ArgumentException>(
            () => sut.UpdateHub("unknown", "name", "http://hub.example", "key"));
    }

    [Fact]
    public async Task UpdateHub_OnDemoHub_Throws()
    {
        var sut = CreateSut();
        var demoId = sut.Hubs.Single(h => h.IsBuiltIn).Id;

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.UpdateHub(demoId, "Hacked", "http://evil.example", "evil-key"));

        sut.Hubs.Single(h => h.IsBuiltIn).BaseUrl.ShouldBe("http://demo.local");
        await _cachingService.DidNotReceive().SaveToCache(CacheKey, Arg.Any<byte[]>());
    }

    // ---------- Remove ----------

    [Fact]
    public async Task RemoveHub_RemovesUserHub_AndFallsBackToDemoWhenActive()
    {
        var sut = CreateSut();
        await sut.AddHub(new HubConfigData("custom-1", "My Hub", "http://my-hub.example", "my-key", IsBuiltIn: false));
        await sut.SelectHub("custom-1");

        await sut.RemoveHub("custom-1");

        sut.Hubs.ShouldHaveSingleItem();
        sut.Hubs.Single().IsBuiltIn.ShouldBeTrue();
        sut.ActiveHubId.ShouldBe(sut.Hubs.Single().Id);
        sut.ActiveBaseUrl.ShouldBe("http://demo.local");
        await _cachingService.Received().SaveToCache(CacheKey, Arg.Any<byte[]>());
    }

    [Fact]
    public async Task RemoveHub_OnDemoHub_Throws()
    {
        var sut = CreateSut();
        var demoId = sut.Hubs.Single(h => h.IsBuiltIn).Id;

        await Should.ThrowAsync<InvalidOperationException>(() => sut.RemoveHub(demoId));

        sut.Hubs.ShouldHaveSingleItem();
        await _cachingService.DidNotReceive().SaveToCache(CacheKey, Arg.Any<byte[]>());
    }

    [Fact]
    public async Task RemoveHub_WithUnknownId_IsNoOp()
    {
        var sut = CreateSut();

        await sut.RemoveHub("unknown");

        sut.Hubs.ShouldHaveSingleItem();
        await _cachingService.DidNotReceive().SaveToCache(CacheKey, Arg.Any<byte[]>());
    }

    // ---------- Persist failure rollback ----------

    [Fact]
    public async Task AddHub_WhenPersistFails_RollsBackAndRethrows()
    {
        var sut = CreateSut();
        _cachingService.SaveToCache(CacheKey, Arg.Any<byte[]>())
            .Returns(Task.FromException(new IOException("disk full")));

        await Should.ThrowAsync<IOException>(() =>
            sut.AddHub(new HubConfigData("custom-1", "My Hub", "http://my-hub.example", "my-key", IsBuiltIn: false)));

        sut.Hubs.ShouldHaveSingleItem();
        sut.Hubs.Single().Id.ShouldBe("demo");
    }

    [Fact]
    public async Task UpdateHub_WhenPersistFails_RollsBackAndRethrows()
    {
        var sut = CreateSut();
        await sut.AddHub(new HubConfigData("custom-1", "My Hub", "http://my-hub.example", "my-key", IsBuiltIn: false));
        _cachingService.SaveToCache(CacheKey, Arg.Any<byte[]>())
            .Returns(Task.FromException(new IOException("disk full")));

        await Should.ThrowAsync<IOException>(() =>
            sut.UpdateHub("custom-1", "Renamed Hub", "http://renamed.example", "renamed-key"));

        var unchanged = sut.Hubs.Single(h => h.Id == "custom-1");
        unchanged.Name.ShouldBe("My Hub");
        unchanged.BaseUrl.ShouldBe("http://my-hub.example");
        unchanged.ApiKey.ShouldBe("my-key");
    }

    [Fact]
    public async Task SelectHub_WhenPersistFails_RollsBackAndRethrows()
    {
        var sut = CreateSut();
        await sut.AddHub(new HubConfigData("custom-1", "My Hub", "http://my-hub.example", "my-key", IsBuiltIn: false));
        _cachingService.SaveToCache(CacheKey, Arg.Any<byte[]>())
            .Returns(Task.FromException(new IOException("disk full")));

        await Should.ThrowAsync<IOException>(() => sut.SelectHub("custom-1"));

        sut.ActiveHubId.ShouldBe(sut.Hubs.Single(h => h.IsBuiltIn).Id);
        sut.ActiveBaseUrl.ShouldBe("http://demo.local");
    }

    [Fact]
    public async Task RemoveHub_WhenPersistFails_RollsBackAndRethrows()
    {
        var sut = CreateSut();
        await sut.AddHub(new HubConfigData("custom-1", "My Hub", "http://my-hub.example", "my-key", IsBuiltIn: false));
        await sut.SelectHub("custom-1");
        _cachingService.SaveToCache(CacheKey, Arg.Any<byte[]>())
            .Returns(Task.FromException(new IOException("disk full")));

        await Should.ThrowAsync<IOException>(() => sut.RemoveHub("custom-1"));

        sut.ActiveHubId.ShouldBe("custom-1");
        sut.ActiveBaseUrl.ShouldBe("http://my-hub.example");
        sut.Hubs.Single(h => h.Id == "custom-1").ShouldNotBeNull();
    }

    [Fact]
    public async Task Mutations_AreSerializedThroughSharedGate()
    {
        var sut = CreateSut();
        await sut.AddHub(new HubConfigData("custom-1", "My Hub", "http://my-hub.example", "my-key", IsBuiltIn: false));
        var persistGate = new TaskCompletionSource();
        _cachingService.SaveToCache(CacheKey, Arg.Any<byte[]>())
            .Returns(persistGate.Task, Task.CompletedTask);

        var first = sut.SelectHub("custom-1");
        var second = sut.RemoveHub("custom-1");

        // The first mutation is blocked in PersistAsync; the second must wait for it
        second.IsCompleted.ShouldBeFalse();

        persistGate.SetResult();
        await first;
        await second;

        sut.Hubs.ShouldHaveSingleItem();
        sut.ActiveHubId.ShouldBe(sut.Hubs.Single(h => h.IsBuiltIn).Id);
    }

    // ---------- Persistence load ----------

    [Fact]
    public async Task Load_LoadsUserHubsAndActiveSelection()
    {
        _cachingService.TryGetCachedFile(CacheKey).Returns(
            SerializeState(
                [new HubConfigData("custom-1", "My Hub", "http://my-hub.example", "my-key", IsBuiltIn: false)],
                "custom-1"));

        var sut = CreateSut();
        await sut.EnsureLoadedAsync();

        sut.Hubs.Count.ShouldBe(2);
        var loaded = sut.Hubs.Single(h => h.Id == "custom-1");
        loaded.IsBuiltIn.ShouldBeFalse();
        loaded.Name.ShouldBe("My Hub");
        sut.ActiveHubId.ShouldBe("custom-1");
        sut.ActiveBaseUrl.ShouldBe("http://my-hub.example");
        sut.ActiveApiKey.ShouldBe("my-key");
    }

    [Fact]
    public async Task Load_WithInvalidActiveSelection_FallsBackToDemoHub()
    {
        _cachingService.TryGetCachedFile(CacheKey).Returns(
            SerializeState(
                [new HubConfigData("custom-1", "My Hub", "http://my-hub.example", "my-key", IsBuiltIn: false)],
                "missing-hub"));

        var sut = CreateSut();
        await sut.EnsureLoadedAsync();

        sut.ActiveHubId.ShouldBe(sut.Hubs.Single(h => h.IsBuiltIn).Id);
        sut.ActiveBaseUrl.ShouldBe("http://demo.local");
    }

    [Fact]
    public async Task Load_WithEmptyCache_DefaultsToDemoHub()
    {
        var sut = CreateSut();
        await sut.EnsureLoadedAsync();

        sut.ActiveHubId.ShouldBe(sut.Hubs.Single(h => h.IsBuiltIn).Id);
        sut.ActiveBaseUrl.ShouldBe("http://demo.local");
    }

    [Fact]
    public async Task Load_WhenCacheThrows_FallsBackToDemoHub()
    {
        _cachingService.TryGetCachedFile(CacheKey)
            .Returns(Task.FromException<byte[]?>(new Exception("read failed")));

        var sut = CreateSut();
        await sut.EnsureLoadedAsync();

        sut.Hubs.ShouldHaveSingleItem();
        sut.ActiveHubId.ShouldBe(sut.Hubs.Single().Id);
    }

    [Fact]
    public async Task PersistedActiveHubReflectsSelection_AcrossNewInstance()
    {
        // First instance: add + select a custom hub.
        var first = CreateSut();
        await first.AddHub(new HubConfigData("custom-1", "My Hub", "http://my-hub.example", "my-key", IsBuiltIn: false));
        await first.SelectHub("custom-1");

        await _cachingService.Received(2).SaveToCache(CacheKey, Arg.Any<byte[]>());

        // Capture what was saved and replay it for a second instance.
        var savedBytes = _cachingService.ReceivedCalls()
            .Last(c => c.GetMethodInfo().Name == nameof(IFileCachingService.SaveToCache))
            .GetArguments()[1] as byte[] ?? throw new InvalidOperationException("No saved payload");
        _cachingService.TryGetCachedFile(CacheKey).Returns(savedBytes);

        var second = CreateSut();
        await second.EnsureLoadedAsync();

        second.Hubs.Count.ShouldBe(2);
        second.ActiveHubId.ShouldBe("custom-1");
        second.ActiveBaseUrl.ShouldBe("http://my-hub.example");
    }

    // ---------- Async accessors ----------

    [Fact]
    public async Task GetActiveHubId_ReturnsActiveHubId()
    {
        _cachingService.TryGetCachedFile(CacheKey).Returns(
            SerializeState(
                [new HubConfigData("custom-1", "My Hub", "http://my-hub.example", "my-key", IsBuiltIn: false)],
                "custom-1"));

        var sut = CreateSut();
        await sut.EnsureLoadedAsync();

        var result = await sut.GetActiveHubId();

        result.ShouldBe("custom-1");
    }

    [Fact]
    public async Task GetHubs_ReturnsBuiltInHubFirstThenUserHubsInNameOrder()
    {
        _cachingService.TryGetCachedFile(CacheKey).Returns(
            SerializeState(
                [
                    new HubConfigData("custom-z", "Zulu Hub", "http://zulu.example", "z-key", IsBuiltIn: false),
                    new HubConfigData("custom-a", "Alpha Hub", "http://alpha.example", "a-key", IsBuiltIn: false)
                ],
                "custom-a"));

        var sut = CreateSut();
        await sut.EnsureLoadedAsync();

        var result = await sut.GetHubs();

        result.Count.ShouldBe(3);
        result[0].IsBuiltIn.ShouldBeTrue();
        result.Select(h => h.Name).ShouldBe(["Demo Hub", "Alpha Hub", "Zulu Hub"]);
    }

    // ---------- Load fallbacks ----------

    [Fact]
    public async Task Load_WithNullCachedData_DefaultsToDemoHub()
    {
        _cachingService.TryGetCachedFile(CacheKey).Returns((byte[]?)null);

        var sut = CreateSut();
        await sut.EnsureLoadedAsync();

        sut.Hubs.ShouldHaveSingleItem();
        sut.Hubs.Single().IsBuiltIn.ShouldBeTrue();
        sut.ActiveHubId.ShouldBe(sut.Hubs.Single().Id);
        sut.ActiveBaseUrl.ShouldBe("http://demo.local");
    }

    [Fact]
    public async Task Load_WithStateWithoutHubs_DefaultsToDemoHub()
    {
        _cachingService.TryGetCachedFile(CacheKey).Returns(SerializeState(null!, null));

        var sut = CreateSut();
        await sut.EnsureLoadedAsync();

        sut.Hubs.ShouldHaveSingleItem();
        sut.Hubs.Single().IsBuiltIn.ShouldBeTrue();
        sut.ActiveHubId.ShouldBe(sut.Hubs.Single().Id);
    }

    [Fact]
    public async Task Load_SkipsBlankAndDuplicateHubIds_KeepsValidEntries()
    {
        _cachingService.TryGetCachedFile(CacheKey).Returns(
            SerializeState(
                [
                    new HubConfigData("", "Blank Id Hub", "http://blank.example", "blank-key", IsBuiltIn: false),
                    new HubConfigData("demo", "Demo Impersonator", "http://evil.example", "evil-key", IsBuiltIn: false),
                    new HubConfigData("custom-1", "Valid Hub", "http://valid.example", "valid-key", IsBuiltIn: false)
                ],
                "custom-1"));

        var sut = CreateSut();
        await sut.EnsureLoadedAsync();

        sut.Hubs.Count.ShouldBe(2);
        sut.Hubs.Single(h => h.Id == "custom-1").Name.ShouldBe("Valid Hub");
        sut.Hubs.ShouldNotContain(h => h.Id == "");
        sut.Hubs.ShouldNotContain(h => h.Id == "demo" && !h.IsBuiltIn);
        sut.ActiveHubId.ShouldBe("custom-1");
    }
}
