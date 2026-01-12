using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.ResourceProviders;
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

public class GitHubResourceStreamProviderTests
{
    private readonly MockHttpMessageHandler _mockHttpMessageHandler;
    private readonly IFileCachingService _cachingService = Substitute.For<IFileCachingService>();
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();
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
        _loggerFactory.CreateLogger<GitHubResourceStreamProvider>().Returns(_logger);

        _sut = new GitHubResourceStreamProvider("mmux", "https://api.github.com/test",
            _cachingService, _loggerFactory,
            httpClient);
    }

    [Fact]
    public void Constructor_ShouldCreateHttpClient_WhenNotProvided()
    {
        // Arrange & Act
        var sut = new GitHubResourceStreamProvider("ext", "url", _cachingService, _loggerFactory);

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
            _loggerFactory,
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
            _loggerFactory,
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
            _loggerFactory,
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
            _loggerFactory,
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
            _loggerFactory,
            httpClient);

        // Act
        var result = await sut.GetResourceStream(testUrl);

        // Assert
        result.ShouldNotBeNull();

        // Verify caching service was called for cache lookup
        await _cachingService.Received(1).TryGetCachedFile(testUrl);

        // Wait for the background caching task to complete
        // The caching happens asynchronously in a fire-and-forget task
        await Task.Delay(100); // Increased delay for CI environments

        // Verify that SaveToCache was called (the background task should have completed)
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
            _loggerFactory,
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
        await _cachingService.DidNotReceive().SaveToCache(Arg.Any<string>(), Arg.Any<byte[]>());
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
            _loggerFactory,
            httpClient);

        // Act
        var result = await sut.GetResourceStream(testUrl);

        // Assert - Should still return content despite caching error
        result.ShouldNotBeNull();
        using var reader = new StreamReader(result);
        var content = await reader.ReadToEndAsync();
        content.ShouldBe(testContent);
    }
}
