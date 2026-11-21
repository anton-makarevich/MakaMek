using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sanet.MakaMek.Assets.ResourceProviders;
using Sanet.MakaMek.Assets.Services;
using Shouldly;

namespace Sanet.MakaMek.Assets.Tests.Services;

public class PackageCacheCoreTests
{
    private sealed class TestCacheState : PackageCacheState;

    private sealed class TestPackageCache() : PackageCacheCore<TestCacheState>([])
    {
        protected override string ResourceKind => "test";

        protected override ILogger Logger => NullLogger.Instance;

        public Task InitializeAsync() => EnsureInitialized();

        // Exposes the protected overload so the non-managed disposal path can be exercised.
        public void DisposeUnmanagedResources() => Dispose(false);

        protected override Task LoadResource(
            IResourceStreamProvider provider,
            string resourceId,
            Stream stream,
            TestCacheState state,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Dispose_DisposesInitializationLock_SoFurtherInitializationThrows()
    {
        // Arrange
        var sut = new TestPackageCache();

        // Act
        sut.Dispose();

        // Assert - the owned semaphore is gone, so lazy initialization can no longer run
        await Should.ThrowAsync<ObjectDisposedException>(() => sut.InitializeAsync());
    }

    [Fact]
    public void Dispose_WhenCalledTwice_DoesNotThrow()
    {
        // Arrange
        var sut = new TestPackageCache();

        // Act & Assert - IDisposable implementations must tolerate repeated disposal
        Should.NotThrow(sut.Dispose);
        Should.NotThrow(sut.Dispose);
    }

    [Fact]
    public async Task DisposeUnmanagedResources_LeavesInitializationLockUsable()
    {
        // Arrange
        var sut = new TestPackageCache();

        // Act
        sut.DisposeUnmanagedResources();

        // Assert - the disposing:false path must not touch managed resources
        await Should.NotThrowAsync(sut.InitializeAsync);
    }
}
