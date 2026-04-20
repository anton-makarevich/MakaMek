using System.Text;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Core.Services;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services;

public class FileSystemCachingServiceTests : IDisposable
{
    private const string TestCacheKey = "test-file-key";
    private readonly byte[] _testContent = "Test file content for caching"u8.ToArray();
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();
    private readonly ILogger<FileSystemCachingService> _logger = Substitute.For<ILogger<FileSystemCachingService>>();
    private readonly FileSystemCachingService _sut;

    public FileSystemCachingServiceTests()
    {
        _loggerFactory.CreateLogger<FileSystemCachingService>().Returns(_logger);
        _sut = new FileSystemCachingService(_loggerFactory);
    }
    
    
    [Fact]
    public async Task TryGetCachedFile_ShouldReturnNull_WhenFileNotCached()
    {
        // Act
        var result = await _sut.TryGetCachedFile("non-existent-key");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task TryGetCachedFile_ShouldReturnNull_WhenCacheKeyIsEmpty()
    {
        // Act
        var result = await _sut.TryGetCachedFile("");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task TryGetCachedFile_ShouldReturnNull_WhenCacheKeyIsNull()
    {
        // Act
        var result = await _sut.TryGetCachedFile(null!);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task SaveToCache_ShouldCacheFile_WhenValidInput()
    {
        // Act
        await _sut.SaveToCache(TestCacheKey, _testContent);

        // Assert
        var isCached = await _sut.IsCached(TestCacheKey);
        isCached.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveToCache_ShouldNotThrow_WhenCacheKeyIsEmpty()
    {
        // Act & Assert
        await Should.NotThrowAsync(async () => await _sut.SaveToCache("", _testContent));
    }

    [Fact]
    public async Task SaveToCache_ShouldNotThrow_WhenContentIsNull()
    {
        // Act & Assert
        await Should.NotThrowAsync(async () => await _sut.SaveToCache(TestCacheKey, null!));
    }

    [Fact]
    public async Task TryGetCachedFile_ShouldReturnCorrectContent_WhenFileCached()
    {
        // Arrange
        await _sut.SaveToCache(TestCacheKey, _testContent);

        // Act
        var result = await _sut.TryGetCachedFile(TestCacheKey);

        // Assert
        result.ShouldNotBeNull();
        using var memoryStream = new MemoryStream();
        result.ShouldBe(_testContent);
    }

    [Fact]
    public async Task IsCached_ShouldReturnFalse_WhenFileNotCached()
    {
        // Act
        var result = await _sut.IsCached("non-existent-key");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsCached_ShouldReturnFalse_WhenCacheKeyIsEmpty()
    {
        // Act
        var result = await _sut.IsCached("");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsCached_ShouldReturnTrue_WhenFileCached()
    {
        // Arrange
        await _sut.SaveToCache(TestCacheKey, _testContent);

        // Act
        var result = await _sut.IsCached(TestCacheKey);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveFromCache_ShouldRemoveFile_WhenFileCached()
    {
        // Arrange
        await _sut.SaveToCache(TestCacheKey, _testContent);
        var isInitiallyCached = await _sut.IsCached(TestCacheKey);
        isInitiallyCached.ShouldBeTrue();

        // Act
        await _sut.RemoveFromCache(TestCacheKey);

        // Assert
        var isCachedAfterRemoval = await _sut.IsCached(TestCacheKey);
        isCachedAfterRemoval.ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveFromCache_ShouldNotThrow_WhenFileNotCached()
    {
        // Act & Assert
        await Should.NotThrowAsync(async () => await _sut.RemoveFromCache("non-existent-key"));
    }

    [Fact]
    public async Task RemoveFromCache_ShouldNotThrow_WhenCacheKeyIsEmpty()
    {
        // Act & Assert
        await Should.NotThrowAsync(async () => await _sut.RemoveFromCache(""));
    }

    [Fact]
    public async Task ClearCache_ShouldRemoveAllFiles()
    {
        // Arrange
        const string key1 = "test-key-1";
        const string key2 = "test-key-2";
        var content1 = "Content 1"u8.ToArray();
        var content2 = "Content 2"u8.ToArray();
        
        await _sut.SaveToCache(key1, content1);
        await _sut.SaveToCache(key2, content2);
        
        var isKey1Cached = await _sut.IsCached(key1);
        var isKey2Cached = await _sut.IsCached(key2);
        isKey1Cached.ShouldBeTrue();
        isKey2Cached.ShouldBeTrue();

        // Act
        await _sut.ClearCache();

        // Assert
        var isKey1CachedAfterClear = await _sut.IsCached(key1);
        var isKey2CachedAfterClear = await _sut.IsCached(key2);
        isKey1CachedAfterClear.ShouldBeFalse();
        isKey2CachedAfterClear.ShouldBeFalse();
    }

    [Fact]
    public async Task ClearCache_ShouldNotThrow_WhenNothingCached()
    {
        // Act & Assert
        await Should.NotThrowAsync(async () => await _sut.ClearCache());
    }

    [Fact]
    public async Task SaveToCache_ShouldOverwriteExistingFile()
    {
        // Arrange
        var originalContent = Encoding.UTF8.GetBytes("Original content");
        var newContent = Encoding.UTF8.GetBytes("New content");
        
        await _sut.SaveToCache(TestCacheKey, originalContent);

        // Act
        await _sut.SaveToCache(TestCacheKey, newContent);

        // Assert
        var result = await _sut.TryGetCachedFile(TestCacheKey);
        result.ShouldNotBeNull();
        result.ShouldBe(newContent);
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldNotCorruptCache()
    {
        // Arrange
        var tasks = new List<Task>();
        var keys = Enumerable.Range(0, 10).Select(i => $"concurrent-key-{i}").ToList();

        // Act - Perform concurrent save operations
        foreach (var key in keys)
        {
            var content = Encoding.UTF8.GetBytes($"Content for {key}");
            tasks.Add(Task.Run(async () =>
            {
                await _sut.SaveToCache(key, content);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All files should be cached
        foreach (var key in keys)
        {
            var isCached = await _sut.IsCached(key);
            isCached.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task SaveToCache_ShouldCreateVersionFile_WhenVersionProvided()
    {
        // Arrange
        const string version = "1.0.0";

        // Act
        await _sut.SaveToCache(TestCacheKey, _testContent, version);

        // Assert
        var retrievedVersion = await _sut.GetCacheVersion(TestCacheKey);
        retrievedVersion.ShouldBe(version);
    }

    [Fact]
    public async Task SaveToCache_ShouldNotCreateVersionFile_WhenVersionIsNull()
    {
        // Arrange
        await _sut.ClearCache();
        
        // Act
        await _sut.SaveToCache(TestCacheKey, _testContent, version: null);

        // Assert
        var retrievedVersion = await _sut.GetCacheVersion(TestCacheKey);
        retrievedVersion.ShouldBeNull();
    }

    [Fact]
    public async Task SaveToCache_ShouldRemoveVersionFile_WhenExistingVersionOverwrittenWithNull()
    {
        // Arrange
        await _sut.ClearCache();
        await _sut.SaveToCache(TestCacheKey, _testContent, "1.0.0");

        // Act
        await _sut.SaveToCache(TestCacheKey, _testContent, version: null);

        // Assert
        var retrievedVersion = await _sut.GetCacheVersion(TestCacheKey);
        retrievedVersion.ShouldBeNull();
    }

    [Fact]
    public async Task SaveToCache_ShouldOverwriteVersionFile_WhenVersionChanged()
    {
        // Arrange
        const string originalVersion = "1.0.0";
        const string newVersion = "2.0.0";

        await _sut.SaveToCache(TestCacheKey, _testContent, originalVersion);

        // Act
        await _sut.SaveToCache(TestCacheKey, _testContent, newVersion);

        // Assert
        var retrievedVersion = await _sut.GetCacheVersion(TestCacheKey);
        retrievedVersion.ShouldBe(newVersion);
    }

    [Fact]
    public async Task GetCacheVersion_ShouldReturnNull_WhenVersionFileDoesNotExist()
    {
        // Arrange
        await _sut.ClearCache();
        await _sut.SaveToCache(TestCacheKey, _testContent, version: null);

        // Act
        var result = await _sut.GetCacheVersion(TestCacheKey);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetCacheVersion_ShouldReturnNull_WhenCacheKeyIsEmpty()
    {
        // Act
        var result = await _sut.GetCacheVersion("");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetCacheVersion_ShouldReturnNull_WhenCacheKeyIsNull()
    {
        // Act
        var result = await _sut.GetCacheVersion(null!);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetCacheVersion_ShouldReturnNull_WhenFileNotCached()
    {
        // Act
        var result = await _sut.GetCacheVersion("non-existent-key");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveFromCache_ShouldRemoveVersionFile_WhenVersionExists()
    {
        // Arrange
        const string version = "1.0.0";
        await _sut.SaveToCache(TestCacheKey, _testContent, version);
        var versionBeforeRemoval = await _sut.GetCacheVersion(TestCacheKey);
        versionBeforeRemoval.ShouldBe(version);

        // Act
        await _sut.RemoveFromCache(TestCacheKey);

        // Assert
        var versionAfterRemoval = await _sut.GetCacheVersion(TestCacheKey);
        versionAfterRemoval.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveFromCache_ShouldNotThrow_WhenVersionFileDoesNotExist()
    {
        // Arrange
        await _sut.SaveToCache(TestCacheKey, _testContent, version: null);

        // Act & Assert
        await Should.NotThrowAsync(async () => await _sut.RemoveFromCache(TestCacheKey));
    }

    [Fact]
    public async Task ClearCache_ShouldRemoveVersionFiles()
    {
        // Arrange
        const string key1 = "test-key-1";
        const string key2 = "test-key-2";
        const string version1 = "1.0.0";
        const string version2 = "2.0.0";
        var content1 = "Content 1"u8.ToArray();
        var content2 = "Content 2"u8.ToArray();
        
        await _sut.SaveToCache(key1, content1, version1);
        await _sut.SaveToCache(key2, content2, version2);
        
        var version1BeforeClear = await _sut.GetCacheVersion(key1);
        var version2BeforeClear = await _sut.GetCacheVersion(key2);
        version1BeforeClear.ShouldBe(version1);
        version2BeforeClear.ShouldBe(version2);

        // Act
        await _sut.ClearCache();

        // Assert
        var version1AfterClear = await _sut.GetCacheVersion(key1);
        var version2AfterClear = await _sut.GetCacheVersion(key2);
        version1AfterClear.ShouldBeNull();
        version2AfterClear.ShouldBeNull();
    }

    public void Dispose()
    {
        _sut.Dispose();
        
        // Clean up any test files that might have been created
        Task.Run(async () =>
        {
            try
            {
                await _sut.ClearCache();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }).Wait(TimeSpan.FromSeconds(5));
    }
}
