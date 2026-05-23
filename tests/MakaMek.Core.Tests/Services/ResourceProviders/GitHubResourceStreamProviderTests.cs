using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Core.Services.ResourceProviders;
using Sanet.MakaMek.Services;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services.ResourceProviders;

public class MockHttpMessageHandler : HttpMessageHandler
{
    public string? ResponseContent { get; set; }
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    public bool IsDisposed { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Ensure async behavior

        if (StatusCode != HttpStatusCode.OK)
        {
            return new HttpResponseMessage
            {
                StatusCode = StatusCode
            };
        }

        var content = ResponseContent ?? string.Empty;
        return new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(content, Encoding.UTF8, "application/octet-stream")
        };
    }

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }
}

public class ThrowingHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new HttpRequestException("Network error");
    }
}

public class ExceptionThrowingHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Some other error");
    }
}

public class UrlMatchingMockHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, string> _urlResponses = new();
    private readonly Dictionary<string, HttpStatusCode> _urlStatusCodes = new();
    private readonly HashSet<string> _throwUrls = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Exception> _throwExceptions = new(StringComparer.Ordinal);

    public void SetResponse(string url, string content)
    {
        _urlResponses[url] = content;
    }

    public void SetStatusCode(string url, HttpStatusCode statusCode)
    {
        _urlStatusCodes[url] = statusCode;
    }

    public void SetThrowUrl(string url)
    {
        _throwUrls.Add(url);
    }

    public void SetThrowException(string url, Exception exception)
    {
        _throwExceptions[url] = exception;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var requestUrl = request.RequestUri?.ToString() ?? string.Empty;

        if (_throwExceptions.TryGetValue(requestUrl, out var exception))
        {
            throw exception;
        }

        if (_throwUrls.Contains(requestUrl))
        {
            throw new HttpRequestException($"Simulated network error for {requestUrl}");
        }

        // Check if we have a specific status code for this URL
        if (_urlStatusCodes.TryGetValue(requestUrl, out var statusCode) && statusCode != HttpStatusCode.OK)
        {
            return new HttpResponseMessage { StatusCode = statusCode };
        }

        // Exact match first; fall back to longest substring match for convenience.
        if (!_urlResponses.TryGetValue(requestUrl, out var content))
        {
            content = _urlResponses
                .Where(kvp => requestUrl.Contains(kvp.Key, StringComparison.Ordinal))
                .OrderByDescending(kvp => kvp.Key.Length)
                .Select(kvp => kvp.Value)
                .FirstOrDefault();
        }

        return new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(content ?? string.Empty, Encoding.UTF8, "application/octet-stream")
        };
    }
}

public class GitHubResourceStreamProviderTests
{
    private readonly MockHttpMessageHandler _mockHttpMessageHandler;
    private readonly IFileCachingService _cachingService = Substitute.For<IFileCachingService>();
    private readonly ILogger<GitHubResourceStreamProvider> _logger = Substitute.For<ILogger<GitHubResourceStreamProvider>>();
    private readonly GitHubResourceStreamProvider _sut;

    public GitHubResourceStreamProviderTests()
    {
        _mockHttpMessageHandler = new MockHttpMessageHandler
        {
            StatusCode = HttpStatusCode.OK
        };

        var httpClient = new HttpClient(_mockHttpMessageHandler)
        {
            BaseAddress = new Uri("https://api.github.com/")
        };
        
        _cachingService.TryGetCachedFile(Arg.Any<string>()).Returns((byte[]?)null);

        _sut = new GitHubResourceStreamProvider("mmux", "https://api.github.com/test",
            _cachingService, _logger,
            httpClient);
    }

    [Fact]
    public void Constructor_ShouldCreateHttpClient_WhenNotProvided()
    {
        // Arrange & Act
        var sut = new GitHubResourceStreamProvider("ext", "url", _cachingService, _logger);

        // Assert - Should not throw and should be able to get resource IDs
        sut.ShouldNotBeNull();
        sut.Dispose();
    }

    [Fact]
    public async Task GetResourceStream_ShouldReturnNull_WhenResourceIdIsEmpty()
    {
        // Arrange & Act
        var result = await _sut.GetResourceStream("");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetResourceStream_ShouldReturnNull_WhenResourceIdIsNull()
    {
        // Arrange & Act
        var result = await _sut.GetResourceStream(null!);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetResourceStream_ShouldReturnStream_WhenValidResourceIdProvided()
    {
        // Arrange
        const string testContent = "test file content";
        _mockHttpMessageHandler.ResponseContent = testContent;

        // Act
        var result = await _sut.GetResourceStream("https://api.github.com/test/file.mmux");

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<MemoryStream>();

        using var reader = new StreamReader(result);
        var content = await reader.ReadToEndAsync();
        content.ShouldBe(testContent);
    }

    [Fact]
    public async Task GetResourceStream_ShouldReturnNull_WhenHttpRequestFails()
    {
        // Arrange
        _mockHttpMessageHandler.StatusCode = HttpStatusCode.NotFound;

        // Act
        var result = await _sut.GetResourceStream("https://api.github.com/test/file.mmux");

        // Assert
        result.ShouldBeNull();
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to download")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void Dispose_ShouldNotDisposeExternallyProvidedHttpClient()
    {
        // Arrange
        var mockHttpMessageHandler = new MockHttpMessageHandler();
        var externalHttpClient = new HttpClient(mockHttpMessageHandler);

        var provider = new GitHubResourceStreamProvider("mmux", "https://api.github.com/test",
            _cachingService,
            _logger,
            externalHttpClient);

        // Act
        provider.Dispose();

        // Assert - External HttpClient should not be disposed
        mockHttpMessageHandler.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldReturnFiles_WhenValidGitHubResponseProvided()
    {
        // Arrange
        const string jsonResponse = """
                                    [
                                        {
                                            "name": "file1.mmux",
                                            "type": "file",
                                            "download_url": "https://raw.githubusercontent.com/test/file1.mmux"
                                        },
                                        {
                                            "name": "file2.mmux",
                                            "type": "file",
                                            "download_url": "https://raw.githubusercontent.com/test/file2.mmux"
                                        },
                                        {
                                            "name": "directory",
                                            "type": "dir",
                                            "download_url": null
                                        },
                                        {
                                            "name": "file.txt",
                                            "type": "file",
                                            "download_url": "https://raw.githubusercontent.com/test/file.txt"
                                        }
                                    ]
                                    """;

        _mockHttpMessageHandler.ResponseContent = jsonResponse;

        // Act
        var result = (await _sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldContain("https://raw.githubusercontent.com/test/file1.mmux");
        result.ShouldContain("https://raw.githubusercontent.com/test/file2.mmux");
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldReturnEmptyList_WhenGitHubResponseIsEmpty()
    {
        // Arrange
        _mockHttpMessageHandler.ResponseContent = "[]";

        // Act
        var result = (await _sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Found 0 mmux files in GitHub repository")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldReturnEmptyList_WhenFilesHaveNoDownloadUrl()
    {
        // Arrange
        const string jsonResponse = """
                                    [
                                        {
                                            "name": "file1.mmux",
                                            "type": "file",
                                            "download_url": null
                                        },
                                        {
                                            "name": "file2.mmux",
                                            "type": "file",
                                            "download_url": null
                                        }
                                    ]
                                    """;

        _mockHttpMessageHandler.ResponseContent = jsonResponse;

        // Act
        var result = (await _sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Found 0 mmux files in GitHub repository")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldReturnEmptyList_WhenHttpStatusCodeIsError()
    {
        // Arrange
        _mockHttpMessageHandler.StatusCode = HttpStatusCode.InternalServerError;

        // Act
        var result = (await _sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to fetch")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
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
            Arg.Is<object>(o => o.ToString()!.Contains("Error loading GitHub contents")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldLogWarning_WhenDeserializationReturnsNull()
    {
        // Arrange
        _mockHttpMessageHandler.ResponseContent = "null";

        // Act
        var result = (await _sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to deserialize GitHub contents response")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldReturnEmptyList_WhenHttpRequestExceptionThrown()
    {
        // Arrange - Create a mock handler that throws HttpRequestException
        var exceptionHandler = new ThrowingHttpMessageHandler();
        var httpClient = new HttpClient(exceptionHandler)
        {
            BaseAddress = new Uri("https://api.github.com/")
        };

        var sut = new GitHubResourceStreamProvider("mmux", "https://api.github.com/test",
            _cachingService,
            _logger,
            httpClient);

        // Act
        var result = (await sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Error loading GitHub contents")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetResourceStream_ShouldReturnNull_WhenHttpRequestExceptionThrown()
    {
        // Arrange - Create a mock handler that throws HttpRequestException
        var exceptionHandler = new ThrowingHttpMessageHandler();
        var httpClient = new HttpClient(exceptionHandler)
        {
            BaseAddress = new Uri("https://api.github.com/")
        };

        var sut = new GitHubResourceStreamProvider("mmux", "https://api.github.com/test",
            _cachingService,
            _logger,
            httpClient);

        // Act
        var result = await sut.GetResourceStream("https://api.github.com/test/file.mmux");

        // Assert
        result.ShouldBeNull();
        _logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Error downloading file from")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetResourceStream_ShouldReturnNull_WhenOtherExceptionThrown()
    {
        // Arrange - Create a mock handler that throws a general exception
        var exceptionHandler = new ExceptionThrowingHttpMessageHandler();
        var httpClient = new HttpClient(exceptionHandler)
        {
            BaseAddress = new Uri("https://api.github.com/")
        };

        var sut = new GitHubResourceStreamProvider("mmux", "https://api.github.com/test",
            _cachingService,
            _logger,
            httpClient);

        // Act
        var result = await sut.GetResourceStream("https://api.github.com/test/file.mmux");

        // Assert
        result.ShouldBeNull();
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Error downloading file from")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetResourceStream_ShouldUseCaching_WhenCachingServiceProvided()
    {
        // Arrange
        const string testContent = "Test file content";
        const string testUrl = "https://api.github.com/test/file.mmux";

        var mockHandler = new MockHttpMessageHandler
        {
            ResponseContent = testContent
        };
        var httpClient = new HttpClient(mockHandler);

        var sut = new GitHubResourceStreamProvider("mmux", "https://api.github.com/test",
            _cachingService,
            _logger,
            httpClient);

        // Act
        var result = await sut.GetResourceStream(testUrl);

        // Assert
        result.ShouldNotBeNull();

        // Verify caching service was called for cache lookup
        await _cachingService.Received(1).TryGetCachedFile(testUrl);

        // Verify that SaveToCache was called
        await _cachingService.Received(1).SaveToCache(testUrl, Arg.Any<byte[]>());
    }

    [Fact]
    public async Task GetResourceStream_ShouldReturnCachedContent_WhenFileIsCached()
    {
        // Arrange
        const string testContent = "Cached file content";
        const string testUrl = "https://api.github.com/test/cached-file.mmux";
        var cachedData = Encoding.UTF8.GetBytes(testContent);

        var mockHandler = new MockHttpMessageHandler(); // Should not be called
        var httpClient = new HttpClient(mockHandler);

        _cachingService.TryGetCachedFile(testUrl).Returns(cachedData);

        var sut = new GitHubResourceStreamProvider("mmux", "https://api.github.com/test",
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

        // Verify that the HTTP client was not used
        mockHandler.ResponseContent.ShouldBeNull();

        // Verify caching service was called
        await _cachingService.Received(1).TryGetCachedFile(testUrl);
        // SaveToCache should NOT be called for the download URL (no network fetch)
        await _cachingService.DidNotReceive().SaveToCache(testUrl, Arg.Any<byte[]>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task GetResourceStream_ShouldHandleCachingErrors_Gracefully()
    {
        // Arrange
        const string testContent = "Test file content";
        const string testUrl = "https://api.github.com/test/file.mmux";

        var mockHandler = new MockHttpMessageHandler
        {
            ResponseContent = testContent
        };
        var httpClient = new HttpClient(mockHandler);

        _cachingService.TryGetCachedFile(testUrl).Returns((byte[]?)null);
        _cachingService.When(x => x.SaveToCache(Arg.Any<string>(), Arg.Any<byte[]>()))
                         .Do(_ => throw new InvalidOperationException("Cache error"));

        var sut = new GitHubResourceStreamProvider("mmux", "https://api.github.com/test",
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
    public async Task GetResourceStream_ShouldDetectShaMismatchAndDownloadFreshContent()
    {
        // Arrange
        const string testContent = "Test file content";
        const string testUrl = "https://api.github.com/test/file.mmux";
        const string cachedSha = "old-sha789";
        const string currentSha = "abc123def456";
        const string apiUrl = "https://api.github.com/test";
        var staleBytes = Encoding.UTF8.GetBytes("Stale content");

        // Set up a URL-matching mock handler
        var mockHandler = new UrlMatchingMockHttpMessageHandler();
        mockHandler.SetResponse(apiUrl, $$"""
            [
                {
                    "name": "file.mmux",
                    "type": "file",
                    "download_url": "{{testUrl}}",
                    "sha": "{{currentSha}}"
                }
            ]
            """);
        mockHandler.SetResponse(testUrl, testContent);

        var httpClient = new HttpClient(mockHandler);

        // Set up the caching service to return a different SHA
        _cachingService.GetCacheVersion(testUrl).Returns(cachedSha);
        _cachingService.TryGetCachedFile(testUrl).Returns(staleBytes);

        var sut = new GitHubResourceStreamProvider("mmux", apiUrl,
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

        // SaveToCache was called with fresh content and new SHA
        await _cachingService.Received(1).SaveToCache(
            testUrl,
            Arg.Is<byte[]>(bytes => Encoding.UTF8.GetString(bytes) == testContent),
            currentSha);

        // Verify log message about cache version mismatch
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Cache version mismatch")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetResourceStream_ShouldServeCachedContent_WhenShaVersionMatches()
    {
        // Arrange
        const string testContent = "Test file content";
        const string testUrl = "https://api.github.com/test/file.mmux";
        const string matchingSha = "abc123def456";
        const string apiUrl = "https://api.github.com/test";

        var mockHandler = new UrlMatchingMockHttpMessageHandler();
        mockHandler.SetResponse(apiUrl, $$"""
            [
                {
                    "name": "file.mmux",
                    "type": "file",
                    "download_url": "{{testUrl}}",
                    "sha": "{{matchingSha}}"
                }
            ]
            """);
        mockHandler.SetResponse(testUrl, testContent);

        var httpClient = new HttpClient(mockHandler);

        // Set up the caching service to return the same SHA
        _cachingService.GetCacheVersion(testUrl).Returns(matchingSha);
        var cachedBytes = Encoding.UTF8.GetBytes("Cached file content");
        _cachingService.TryGetCachedFile(testUrl).Returns(cachedBytes);

        var sut = new GitHubResourceStreamProvider("mmux", apiUrl,
            _cachingService,
            _logger,
            httpClient);

        // Act
        var result = await sut.GetResourceStream(testUrl);

        // Assert
        result.ShouldNotBeNull();
        using var reader = new StreamReader(result);
        var content = await reader.ReadToEndAsync();
        content.ShouldBe("Cached file content");

        // Verify that GetCacheVersion was called but no staleness checks beyond that
        await _cachingService.Received(1).GetCacheVersion(testUrl);
        await _cachingService.DidNotReceive().IsCached(Arg.Any<string>());
        await _cachingService.DidNotReceive().RemoveFromCache(Arg.Any<string>());
        await _cachingService.DidNotReceive().SaveToCache(testUrl, Arg.Any<byte[]>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task GetResourceStream_ShouldServeStaleCache_WhenShaMismatchAndDownloadFails()
    {
        // Arrange
        const string testContent = "Stale content";
        const string testUrl = "https://api.github.com/test/file.mmux";
        const string cachedSha = "old-sha789";
        const string currentSha = "abc123def456";
        const string apiUrl = "https://api.github.com/test";
        var staleBytes = Encoding.UTF8.GetBytes(testContent);

        var mockHandler = new UrlMatchingMockHttpMessageHandler();
        mockHandler.SetResponse(apiUrl, $$"""
            [
                {
                    "name": "file.mmux",
                    "type": "file",
                    "download_url": "{{testUrl}}",
                    "sha": "{{currentSha}}"
                }
            ]
            """);
        mockHandler.SetStatusCode(testUrl, HttpStatusCode.NotFound);

        var httpClient = new HttpClient(mockHandler);

        _cachingService.GetCacheVersion(testUrl).Returns(cachedSha);
        _cachingService.TryGetCachedFile(testUrl).Returns(staleBytes);

        var sut = new GitHubResourceStreamProvider("mmux", apiUrl,
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

        // Verify stale cache fallback was logged
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Serving stale cached content")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());

        // Verify no SaveToCache was attempted for the download URL (download failed)
        await _cachingService.DidNotReceive().SaveToCache(testUrl, Arg.Any<byte[]>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task GetResourceStream_ShouldReturnCachedContent_WhenNetworkIsUnavailable()
    {
        // Arrange
        const string testUrl = "https://api.github.com/test/file.mmux";
        var staleBytes = Encoding.UTF8.GetBytes("Cached content");

        var exceptionHandler = new ThrowingHttpMessageHandler();
        var httpClient = new HttpClient(exceptionHandler)
        {
            BaseAddress = new Uri("https://api.github.com/")
        };

        _cachingService.TryGetCachedFile(testUrl).Returns(staleBytes);

        var sut = new GitHubResourceStreamProvider("mmux", "https://api.github.com/test",
            _cachingService,
            _logger,
            httpClient);

        // Act
        var result = await sut.GetResourceStream(testUrl);

        // Assert
        result.ShouldNotBeNull();
        using var reader = new StreamReader(result);
        var content = await reader.ReadToEndAsync();
        content.ShouldBe("Cached content");
    }

    [Fact]
    public async Task GetResourceStream_ShouldLogAndServeStaleCache_WhenDownloadThrowsAndCacheAvailable()
    {
        // Arrange
        const string testContent = "Stale cached content";
        const string testUrl = "https://api.github.com/test/file.mmux";
        const string cachedSha = "old-sha789";
        const string currentSha = "abc123def456";
        const string apiUrl = "https://api.github.com/test";
        var staleBytes = Encoding.UTF8.GetBytes(testContent);

        var mockHandler = new UrlMatchingMockHttpMessageHandler();
        mockHandler.SetResponse(apiUrl, $$"""
            [
                {
                    "name": "file.mmux",
                    "type": "file",
                    "download_url": "{{testUrl}}",
                    "sha": "{{currentSha}}"
                }
            ]
            """);
        mockHandler.SetThrowUrl(testUrl);

        var httpClient = new HttpClient(mockHandler);

        _cachingService.GetCacheVersion(testUrl).Returns(cachedSha);
        _cachingService.TryGetCachedFile(testUrl).Returns(staleBytes);

        var sut = new GitHubResourceStreamProvider("mmux", apiUrl,
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

        // Verify "Serving stale cached content" is logged (line 135)
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Serving stale cached content")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetResourceStream_ShouldLogAndServeStaleCache_WhenDownloadThrowsGeneralExceptionAndCacheAvailable()
    {
        // Arrange
        const string testContent = "Stale cached content";
        const string testUrl = "https://api.github.com/test/file.mmux";
        const string cachedSha = "old-sha789";
        const string currentSha = "abc123def456";
        const string apiUrl = "https://api.github.com/test";
        var staleBytes = Encoding.UTF8.GetBytes(testContent);

        var mockHandler = new UrlMatchingMockHttpMessageHandler();
        mockHandler.SetResponse(apiUrl, $$"""
            [
                {
                    "name": "file.mmux",
                    "type": "file",
                    "download_url": "{{testUrl}}",
                    "sha": "{{currentSha}}"
                }
            ]
            """);
        mockHandler.SetThrowException(testUrl, new InvalidOperationException("Some other error"));

        var httpClient = new HttpClient(mockHandler);

        _cachingService.GetCacheVersion(testUrl).Returns(cachedSha);
        _cachingService.TryGetCachedFile(testUrl).Returns(staleBytes);

        var sut = new GitHubResourceStreamProvider("mmux", apiUrl,
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

        // Verify "Serving stale cached content" is logged (line 135)
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Serving stale cached content")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldCacheApiManifest()
    {
        // Arrange
        const string jsonResponse = """
                                    [
                                        {
                                            "name": "file1.mmux",
                                            "type": "file",
                                            "download_url": "https://raw.githubusercontent.com/test/file1.mmux",
                                            "sha": "sha123"
                                        }
                                    ]
                                    """;

        _mockHttpMessageHandler.ResponseContent = jsonResponse;

        // Act
        var result = (await _sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);

        await _cachingService.Received(1).SaveToCache(
            "https://api.github.com/test",
            Arg.Is<byte[]>(bytes => Encoding.UTF8.GetString(bytes) == jsonResponse));
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldLogError_WhenCachingApiManifestThrows()
    {
        // Arrange
        const string jsonResponse = """
                                    [
                                        {
                                            "name": "file1.mmux",
                                            "type": "file",
                                            "download_url": "https://raw.githubusercontent.com/test/file1.mmux",
                                            "sha": "sha123"
                                        }
                                    ]
                                    """;

        _mockHttpMessageHandler.ResponseContent = jsonResponse;
        _cachingService.When(x => x.SaveToCache("https://api.github.com/test", Arg.Any<byte[]>()))
                         .Do(_ => throw new InvalidOperationException("Cache error"));

        // Act
        var result = (await _sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Error caching API manifest for")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetAvailableResourceIds_ShouldUseCachedManifest_WhenHttpFails()
    {
        // Arrange
        const string cachedJson = """
                                  [
                                      {
                                          "name": "file1.mmux",
                                          "type": "file",
                                          "download_url": "https://raw.githubusercontent.com/test/file1.mmux",
                                          "sha": "sha456"
                                      },
                                      {
                                          "name": "file2.mmux",
                                          "type": "file",
                                          "download_url": "https://raw.githubusercontent.com/test/file2.mmux",
                                          "sha": "sha789"
                                      }
                                  ]
                                  """;

        _mockHttpMessageHandler.StatusCode = HttpStatusCode.InternalServerError;
        _cachingService.TryGetCachedFile("https://api.github.com/test")
            .Returns(Encoding.UTF8.GetBytes(cachedJson));

        // Act
        var result = (await _sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldContain("https://raw.githubusercontent.com/test/file1.mmux");
        result.ShouldContain("https://raw.githubusercontent.com/test/file2.mmux");

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Using cached API manifest")),
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
    public async Task GetAvailableResourceIds_ShouldLogError_WhenCachedManifestThrowsException()
    {
        // Arrange
        _mockHttpMessageHandler.StatusCode = HttpStatusCode.InternalServerError;

        _cachingService.TryGetCachedFile("https://api.github.com/test")
            .Returns(Task.FromException<byte[]?>(new InvalidOperationException("Cache error")));

        // Act
        var result = (await _sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();

        // Verify "Error loading cached API manifest" is logged (lines 224-227)
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Error loading cached API manifest")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
