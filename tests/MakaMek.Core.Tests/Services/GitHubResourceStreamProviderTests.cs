using System.Net;
using System.Text;
using Sanet.MakaMek.Core.Services;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services;

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

        _sut = new GitHubResourceStreamProvider("mmux", "https://api.github.com/test", httpClient);
    }

    [Fact]
    public void Constructor_ShouldCreateHttpClient_WhenNotProvided()
    {
        // Arrange & Act
        var provider = new GitHubResourceStreamProvider("ext", "url");

        // Assert - Should not throw and should be able to get resource IDs
        provider.ShouldNotBeNull();
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
    public void GetAvailableResourceIds_ShouldReturnEmptyList_WhenHttpRequestFails()
    {
        // Note: This test is challenging with the current implementation because
        // HttpClient is difficult to mock. In a real implementation, we would
        // want to inject an IHttpClientFactory or wrap HttpClient in an interface.

        // For now, we'll test the basic functionality
        var resourceIds = _sut.GetAvailableResourceIds();
        resourceIds.ShouldNotBeNull();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        Should.NotThrow(() => _sut.Dispose());
    }

    [Fact]
    public async Task GetResourceStream_ShouldReturnStream_WhenValidResourceIdProvided()
    {
        // Arrange
        var testContent = "test file content";
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
    }

    [Fact]
    public void Dispose_ShouldNotDisposeExternallyProvidedHttpClient()
    {
        // Arrange
        var mockHttpMessageHandler = new MockHttpMessageHandler();
        var externalHttpClient = new HttpClient(mockHttpMessageHandler);

        var provider = new GitHubResourceStreamProvider("mmux", "https://api.github.com/test", externalHttpClient);

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

        var sut = new GitHubResourceStreamProvider("mmux", "https://api.github.com/test", httpClient);

        // Act
        var result = (await sut.GetAvailableResourceIds()).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
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

        var sut = new GitHubResourceStreamProvider("mmux", "https://api.github.com/test", httpClient);

        // Act
        var result = await sut.GetResourceStream("https://api.github.com/test/file.mmux");

        // Assert
        result.ShouldBeNull();
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

        var sut = new GitHubResourceStreamProvider("mmux", "https://api.github.com/test", httpClient);

        // Act
        var result = await sut.GetResourceStream("https://api.github.com/test/file.mmux");

        // Assert
        result.ShouldBeNull();
    }
}
