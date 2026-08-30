using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Services;
using Sanet.MakaMek.Services.ResourceProviders;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services.ResourceProviders;

public class BucketResourceStreamProviderTests
{
    private const string BaseUrl = "https://data.makamek.nl";
    private const string ManifestUrl = $"{BaseUrl}/manifest.json";

    private readonly IFileCachingService _cachingService;
    private readonly ILogger<BucketResourceStreamProvider> _logger;
    private readonly MockHttpMessageHandler _mockHttpMessageHandler;
    private readonly BucketResourceStreamProvider _sut;

    public BucketResourceStreamProviderTests()
    {
        _cachingService = Substitute.For<IFileCachingService>();
        _cachingService.TryGetCachedFile(Arg.Any<string>()).Returns((byte[]?)null);
        _logger = Substitute.For<ILogger<BucketResourceStreamProvider>>();
        _mockHttpMessageHandler = new MockHttpMessageHandler();
        _sut = new BucketResourceStreamProvider("units/mechs", "mmux", BaseUrl,
            _cachingService,
            _logger,
            new HttpClient(_mockHttpMessageHandler));
    }

    private static string CreateManifestJson(
        string path = "units/mechs/commando.mmux",
        string name = "commando.mmux",
        string hash = "hash123",
        string url = $"{BaseUrl}/units/mechs/commando.mmux")
    {
        return $$"""
            {
                "version": "1.2.3",
                "generatedAtUtc": "2026-08-30T10:00:00.000Z",
                "fileCount": 1,
                "files": [
                    {
                        "path": "{{path}}",
                        "name": "{{name}}",
                        "hash": "{{hash}}",
                        "url": "{{url}}"
                    }
                ]
            }
            """;
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldReturnFiles_WhenValidManifestProvided()
    {
        // Arrange
        _mockHttpMessageHandler.ResponseContent = CreateManifestJson();

        // Act
        var result = (await _sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result.ShouldContain($"{BaseUrl}/units/mechs/commando.mmux");
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldFilterByPathPrefix()
    {
        // Arrange
        _mockHttpMessageHandler.ResponseContent = $$"""
            {
                "version": "1.2.3",
                "generatedAtUtc": "2026-08-30T10:00:00.000Z",
                "fileCount": 2,
                "files": [
                    {
                        "path": "units/mechs/commando.mmux",
                        "name": "commando.mmux",
                        "hash": "hash123",
                        "url": "{{BaseUrl}}/units/mechs/commando.mmux"
                    },
                    {
                        "path": "hexes/biomes/grass.mmtx",
                        "name": "grass.mmtx",
                        "hash": "hash456",
                        "url": "{{BaseUrl}}/hexes/biomes/grass.mmtx"
                    }
                ]
            }
            """;

        // Act
        var result = (await _sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result.ShouldContain($"{BaseUrl}/units/mechs/commando.mmux");
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldFilterByExtension()
    {
        // Arrange
        _mockHttpMessageHandler.ResponseContent = $$"""
            {
                "version": "1.2.3",
                "generatedAtUtc": "2026-08-30T10:00:00.000Z",
                "fileCount": 2,
                "files": [
                    {
                        "path": "units/mechs/commando.mmux",
                        "name": "commando.mmux",
                        "hash": "hash123",
                        "url": "{{BaseUrl}}/units/mechs/commando.mmux"
                    },
                    {
                        "path": "units/mechs/readme.txt",
                        "name": "readme.txt",
                        "hash": "hash456",
                        "url": "{{BaseUrl}}/units/mechs/readme.txt"
                    }
                ]
            }
            """;

        // Act
        var result = (await _sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result.ShouldContain($"{BaseUrl}/units/mechs/commando.mmux");
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldTrimTrailingSlashFromBaseUrl()
    {
        // Arrange
        var mockHandler = new UrlMatchingMockHttpMessageHandler();
        mockHandler.SetResponse(ManifestUrl, CreateManifestJson());
        var sut = new BucketResourceStreamProvider("units/mechs", "mmux", $"{BaseUrl}/",
            _cachingService,
            _logger,
            new HttpClient(mockHandler));

        // Act
        var result = (await sut.GetAvailableResourceIds()).ToList();

        // Assert - manifest was fetched from the exact ManifestUrl (no double slash)
        result.ShouldNotBeNull();
        result.ShouldContain($"{BaseUrl}/units/mechs/commando.mmux");
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldReturnEmptyList_WhenFilesHaveNoUrl()
    {
        // Arrange
        _mockHttpMessageHandler.ResponseContent = """
            {
                "version": "1.2.3",
                "generatedAtUtc": "2026-08-30T10:00:00.000Z",
                "fileCount": 1,
                "files": [
                    {
                        "path": "units/mechs/commando.mmux",
                        "name": "commando.mmux",
                        "hash": "hash123",
                        "url": ""
                    }
                ]
            }
            """;

        // Act
        var result = (await _sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldReturnEmptyList_WhenManifestIsEmpty()
    {
        // Arrange
        _mockHttpMessageHandler.ResponseContent = """
            {
                "version": "1.2.3",
                "generatedAtUtc": "2026-08-30T10:00:00.000Z",
                "fileCount": 0,
                "files": []
            }
            """;

        // Act
        var result = (await _sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldCacheManifest()
    {
        // Arrange
        var jsonResponse = CreateManifestJson();
        _mockHttpMessageHandler.ResponseContent = jsonResponse;

        // Act
        var result = (await _sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);

        await _cachingService.Received(1).SaveToCache(
            ManifestUrl,
            Arg.Is<byte[]>(bytes => Encoding.UTF8.GetString(bytes) == jsonResponse));
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldLogError_WhenCachingManifestThrows()
    {
        // Arrange
        _mockHttpMessageHandler.ResponseContent = CreateManifestJson();
        _cachingService.When(x => x.SaveToCache(ManifestUrl, Arg.Any<byte[]>()))
                         .Do(_ => throw new InvalidOperationException("Cache error"));

        // Act
        var result = (await _sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Error caching manifest for")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldUseCachedManifest_WhenHttpFails()
    {
        // Arrange
        var cachedJson = $$"""
            {
                "version": "1.2.3",
                "generatedAtUtc": "2026-08-30T10:00:00.000Z",
                "fileCount": 2,
                "files": [
                    {
                        "path": "units/mechs/commando.mmux",
                        "name": "commando.mmux",
                        "hash": "hash456",
                        "url": "{{BaseUrl}}/units/mechs/commando.mmux"
                    },
                    {
                        "path": "units/mechs/stinger.mmux",
                        "name": "stinger.mmux",
                        "hash": "hash789",
                        "url": "{{BaseUrl}}/units/mechs/stinger.mmux"
                    }
                ]
            }
            """;

        _mockHttpMessageHandler.StatusCode = HttpStatusCode.InternalServerError;
        _cachingService.TryGetCachedFile(ManifestUrl)
            .Returns(Encoding.UTF8.GetBytes(cachedJson));

        // Act
        var result = (await _sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldContain($"{BaseUrl}/units/mechs/commando.mmux");
        result.ShouldContain($"{BaseUrl}/units/mechs/stinger.mmux");

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Using cached manifest")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldReturnEmptyList_WhenHttpFailsAndNoCachedManifest()
    {
        // Arrange
        _mockHttpMessageHandler.StatusCode = HttpStatusCode.InternalServerError;

        // Act
        var result = (await _sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldReturnEmptyList_WhenJsonParsingFails()
    {
        // Arrange
        _mockHttpMessageHandler.ResponseContent = "invalid json content";

        // Act
        var result = (await _sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Error loading bucket manifest")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetResourceStream_ShouldReturnNull_WhenResourceIdIsEmpty()
    {
        // Act
        var result = await _sut.GetResourceStream(string.Empty);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetResourceStream_ShouldDetectHashMismatchAndDownloadFreshContent()
    {
        // Arrange
        const string testContent = "Test file content";
        const string testUrl = $"{BaseUrl}/units/mechs/commando.mmux";
        const string cachedHash = "old-hash";
        const string currentHash = "hash123";

        var mockHandler = new UrlMatchingMockHttpMessageHandler();
        mockHandler.SetResponse(ManifestUrl, CreateManifestJson(hash: currentHash, url: testUrl));
        mockHandler.SetResponse(testUrl, testContent);

        var httpClient = new HttpClient(mockHandler);

        // Set up the caching service to return a different hash
        _cachingService.GetCacheVersion(testUrl).Returns(cachedHash);
        _cachingService.TryGetCachedFile(testUrl).Returns(Encoding.UTF8.GetBytes("Stale content"));

        var sut = new BucketResourceStreamProvider("units/mechs", "mmux", BaseUrl,
            _cachingService,
            _logger,
            httpClient);

        // Act
        var result = await sut.GetResourceStream(testUrl);

        // Assert
        result.ShouldNotBeNull();
        using var reader = new StreamReader(result);
        var content = await reader.ReadToEndAsync();
        content.ShouldBe(testContent);

        // Verify staleness was detected but cache was NOT evicted
        await _cachingService.Received(1).GetCacheVersion(testUrl);
        await _cachingService.DidNotReceive().IsCached(Arg.Any<string>());
        await _cachingService.DidNotReceive().RemoveFromCache(Arg.Any<string>());

        // SaveToCache was called with fresh content and new hash
        await _cachingService.Received(1).SaveToCache(
            testUrl,
            Arg.Is<byte[]>(bytes => Encoding.UTF8.GetString(bytes) == testContent),
            currentHash);
    }

    [Fact]
    public async Task GetResourceStream_ShouldServeCachedContent_WhenHashVersionMatches()
    {
        // Arrange
        const string testContent = "Test file content";
        const string testUrl = $"{BaseUrl}/units/mechs/commando.mmux";
        const string matchingHash = "hash123";

        var mockHandler = new UrlMatchingMockHttpMessageHandler();
        mockHandler.SetResponse(ManifestUrl, CreateManifestJson(hash: matchingHash, url: testUrl));

        var httpClient = new HttpClient(mockHandler);

        _cachingService.GetCacheVersion(testUrl).Returns(matchingHash);
        _cachingService.TryGetCachedFile(testUrl).Returns(Encoding.UTF8.GetBytes(testContent));

        var sut = new BucketResourceStreamProvider("units/mechs", "mmux", BaseUrl,
            _cachingService,
            _logger,
            httpClient);

        // Act
        var result = await sut.GetResourceStream(testUrl);

        // Assert
        result.ShouldNotBeNull();
        using var reader = new StreamReader(result);
        var content = await reader.ReadToEndAsync();
        content.ShouldBe(testContent);

        // Verify that the HTTP client was not used for the file download
        await _cachingService.Received(1).GetCacheVersion(testUrl);
        await _cachingService.Received(1).TryGetCachedFile(testUrl);
        await _cachingService.DidNotReceive().SaveToCache(
            testUrl,
            Arg.Any<byte[]>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task GetResourceStream_ShouldServeCachedContent_WhenHashIsMissing()
    {
        // Arrange
        const string testContent = "Test file content";
        const string testUrl = $"{BaseUrl}/units/mechs/commando.mmux";

        var mockHandler = new UrlMatchingMockHttpMessageHandler();
        mockHandler.SetResponse(ManifestUrl, CreateManifestJson(hash: "", url: testUrl));

        var httpClient = new HttpClient(mockHandler);

        _cachingService.TryGetCachedFile(testUrl).Returns(Encoding.UTF8.GetBytes(testContent));

        var sut = new BucketResourceStreamProvider("units/mechs", "mmux", BaseUrl,
            _cachingService,
            _logger,
            httpClient);

        // Act
        var result = await sut.GetResourceStream(testUrl);

        // Assert - missing hash means no version check, cached content is served directly
        result.ShouldNotBeNull();
        using var reader = new StreamReader(result);
        var content = await reader.ReadToEndAsync();
        content.ShouldBe(testContent);

        await _cachingService.DidNotReceive().GetCacheVersion(Arg.Any<string>());
        await _cachingService.DidNotReceive().SaveToCache(
            testUrl,
            Arg.Any<byte[]>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task GetResourceStream_ShouldDownloadFreshContent_WhenHashIsMissingAndNoCache()
    {
        // Arrange
        const string testContent = "Test file content";
        const string testUrl = $"{BaseUrl}/units/mechs/commando.mmux";

        var mockHandler = new UrlMatchingMockHttpMessageHandler();
        mockHandler.SetResponse(ManifestUrl, CreateManifestJson(hash: "", url: testUrl));
        mockHandler.SetResponse(testUrl, testContent);

        var httpClient = new HttpClient(mockHandler);

        var sut = new BucketResourceStreamProvider("units/mechs", "mmux", BaseUrl,
            _cachingService,
            _logger,
            httpClient);

        // Act
        var result = await sut.GetResourceStream(testUrl);

        // Assert
        result.ShouldNotBeNull();
        using var reader = new StreamReader(result);
        var content = await reader.ReadToEndAsync();
        content.ShouldBe(testContent);

        // SaveToCache is called without a version when the hash is missing
        await _cachingService.Received(1).SaveToCache(
            testUrl,
            Arg.Is<byte[]>(bytes => Encoding.UTF8.GetString(bytes) == testContent),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task GetResourceStream_ShouldServeStaleCache_WhenHashMismatchAndDownloadFails()
    {
        // Arrange
        const string testContent = "Stale cached content";
        const string testUrl = $"{BaseUrl}/units/mechs/commando.mmux";
        const string cachedHash = "old-hash";
        const string currentHash = "hash123";

        var mockHandler = new UrlMatchingMockHttpMessageHandler();
        mockHandler.SetResponse(ManifestUrl, CreateManifestJson(hash: currentHash, url: testUrl));
        mockHandler.SetStatusCode(testUrl, HttpStatusCode.InternalServerError);

        var httpClient = new HttpClient(mockHandler);

        _cachingService.GetCacheVersion(testUrl).Returns(cachedHash);
        _cachingService.TryGetCachedFile(testUrl).Returns(Encoding.UTF8.GetBytes(testContent));

        var sut = new BucketResourceStreamProvider("units/mechs", "mmux", BaseUrl,
            _cachingService,
            _logger,
            httpClient);

        // Act
        var result = await sut.GetResourceStream(testUrl);

        // Assert
        result.ShouldNotBeNull();
        using var reader = new StreamReader(result);
        var content = await reader.ReadToEndAsync();
        content.ShouldBe(testContent);

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Serving stale cached content")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetResourceStream_ShouldReturnNull_WhenHashMismatchDownloadFailsAndNoCache()
    {
        // Arrange
        const string testUrl = $"{BaseUrl}/units/mechs/commando.mmux";
        const string cachedHash = "old-hash";
        const string currentHash = "hash123";

        var mockHandler = new UrlMatchingMockHttpMessageHandler();
        mockHandler.SetResponse(ManifestUrl, CreateManifestJson(hash: currentHash, url: testUrl));
        mockHandler.SetStatusCode(testUrl, HttpStatusCode.NotFound);

        var httpClient = new HttpClient(mockHandler);

        _cachingService.GetCacheVersion(testUrl).Returns(cachedHash);

        var sut = new BucketResourceStreamProvider("units/mechs", "mmux", BaseUrl,
            _cachingService,
            _logger,
            httpClient);

        // Act
        var result = await sut.GetResourceStream(testUrl);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetResourceStream_ShouldServeStaleCache_WhenDownloadThrows()
    {
        // Arrange
        const string testContent = "Stale cached content";
        const string testUrl = $"{BaseUrl}/units/mechs/commando.mmux";
        const string cachedHash = "old-hash";
        const string currentHash = "hash123";

        var mockHandler = new UrlMatchingMockHttpMessageHandler();
        mockHandler.SetResponse(ManifestUrl, CreateManifestJson(hash: currentHash, url: testUrl));
        mockHandler.SetThrowUrl(testUrl);

        var httpClient = new HttpClient(mockHandler);

        _cachingService.GetCacheVersion(testUrl).Returns(cachedHash);
        _cachingService.TryGetCachedFile(testUrl).Returns(Encoding.UTF8.GetBytes(testContent));

        var sut = new BucketResourceStreamProvider("units/mechs", "mmux", BaseUrl,
            _cachingService,
            _logger,
            httpClient);

        // Act
        var result = await sut.GetResourceStream(testUrl);

        // Assert
        result.ShouldNotBeNull();
        using var reader = new StreamReader(result);
        var content = await reader.ReadToEndAsync();
        content.ShouldBe(testContent);

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Serving stale cached content")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetResourceStream_ShouldHandleCachingErrors_Gracefully()
    {
        // Arrange
        const string testContent = "Test file content";
        const string testUrl = $"{BaseUrl}/units/mechs/commando.mmux";

        var mockHandler = new UrlMatchingMockHttpMessageHandler();
        mockHandler.SetResponse(ManifestUrl, CreateManifestJson(url: testUrl));
        mockHandler.SetResponse(testUrl, testContent);

        var httpClient = new HttpClient(mockHandler);

        _cachingService.TryGetCachedFile(testUrl).Returns((byte[]?)null);
        _cachingService.When(x => x.SaveToCache(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string?>()))
                         .Do(_ => throw new InvalidOperationException("Cache error"));

        var sut = new BucketResourceStreamProvider("units/mechs", "mmux", BaseUrl,
            _cachingService,
            _logger,
            httpClient);

        // Act
        var result = await sut.GetResourceStream(testUrl);

        // Assert - Should still return content despite caching error
        result.ShouldNotBeNull();
        using var reader = new StreamReader(result);
        var content = await reader.ReadToEndAsync();
        content.ShouldBe(testContent);
    }

    [Fact]
    public async Task GetResourceStream_ShouldUseHashAsResourceVersion()
    {
        // Arrange
        const string testContent = "Test file content";
        const string testUrl = $"{BaseUrl}/units/mechs/commando.mmux";
        const string manifestHash = "hash123";

        var mockHandler = new UrlMatchingMockHttpMessageHandler();
        mockHandler.SetResponse(ManifestUrl, CreateManifestJson(hash: manifestHash, url: testUrl));
        mockHandler.SetResponse(testUrl, testContent);

        var httpClient = new HttpClient(mockHandler);

        var sut = new BucketResourceStreamProvider("units/mechs", "mmux", BaseUrl,
            _cachingService,
            _logger,
            httpClient);

        // Act
        var result = await sut.GetResourceStream(testUrl);

        // Assert - version check happens against the manifest hash
        await _cachingService.Received(1).GetCacheVersion(testUrl);
        await _cachingService.Received(1).SaveToCache(
            testUrl,
            Arg.Any<byte[]>(),
            manifestHash);
        result.ShouldNotBeNull();
    }

    [Fact]
    public void Dispose_ShouldNotDisposeExternallyProvidedHttpClient()
    {
        // Arrange
        var mockHttpMessageHandler = new MockHttpMessageHandler();
        var externalHttpClient = new HttpClient(mockHttpMessageHandler);

        var sut = new BucketResourceStreamProvider("units/mechs", "mmux", BaseUrl,
            _cachingService,
            _logger,
            externalHttpClient);

        // Act
        sut.Dispose();

        // Assert - External HttpClient should not be disposed
        mockHttpMessageHandler.IsDisposed.ShouldBeFalse();
    }
}
