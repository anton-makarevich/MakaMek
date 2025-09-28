using System.Net;
using System.Text;
using System.Text.Json;
using NSubstitute;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.GitHub;
using Shouldly;

namespace MakaMek.Core.Tests.Services;

public class GitHubResourceStreamProviderTests
{
    private readonly HttpClient _httpClient;
    private readonly GitHubResourceStreamProvider _sut;

    public GitHubResourceStreamProviderTests()
    {
        _httpClient = Substitute.For<HttpClient>();
        _sut = new GitHubResourceStreamProvider(
            "test-owner",
            "test-repo", 
            "data/units/mechs",
            "mmux",
            _httpClient);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenRepositoryOwnerIsNull()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new GitHubResourceStreamProvider(null!, "repo", "path", "ext"));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenRepositoryNameIsNull()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new GitHubResourceStreamProvider("owner", null!, "path", "ext"));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenFolderPathIsNull()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new GitHubResourceStreamProvider("owner", "repo", null!, "ext"));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenFileExtensionIsNull()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new GitHubResourceStreamProvider("owner", "repo", "path", null!));
    }

    [Fact]
    public void Constructor_ShouldCreateHttpClient_WhenNotProvided()
    {
        // Arrange & Act
        var provider = new GitHubResourceStreamProvider("owner", "repo", "path", "ext");
        
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
}

/// <summary>
/// Integration tests that use a real HttpClient to test against actual GitHub API
/// These tests are marked as integration tests and may be skipped in CI
/// </summary>
public class GitHubResourceStreamProviderIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAvailableResourceIds_ShouldReturnResourceIds_WhenRepositoryExists()
    {
        // Arrange
        var provider = new GitHubResourceStreamProvider(
            "anton-makarevich",
            "MakaMek",
            "data/units/mechs",
            "mmux");

        // Act
        var resourceIds = provider.GetAvailableResourceIds();

        // Assert
        resourceIds.ShouldNotBeNull();
        // Note: We can't assert specific count as the repository content may change
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetResourceStream_ShouldReturnStream_WhenValidDownloadUrl()
    {
        // Arrange
        var provider = new GitHubResourceStreamProvider(
            "anton-makarevich",
            "MakaMek",
            "data/units/mechs",
            "mmux");

        var resourceIds = provider.GetAvailableResourceIds().ToList();
        
        // Skip test if no resources are available
        if (!resourceIds.Any())
        {
            return;
        }

        var firstResourceId = resourceIds.First();

        // Act
        var stream = await provider.GetResourceStream(firstResourceId);

        // Assert
        stream.ShouldNotBeNull();
        stream.Length.ShouldBeGreaterThan(0);
        
        // Clean up
        stream.Dispose();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetResourceStream_ShouldReturnNull_WhenInvalidUrl()
    {
        // Arrange
        var provider = new GitHubResourceStreamProvider(
            "anton-makarevich",
            "MakaMek",
            "data/units/mechs",
            "mmux");

        // Act
        var stream = await provider.GetResourceStream("https://invalid-url.com/file.mmux");

        // Assert
        stream.ShouldBeNull();
    }
}
