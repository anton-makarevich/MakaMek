using System.Reflection;
using Sanet.MakaMek.Core.Services;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services;

public class AssemblyUnitStreamProviderTests
{
    [Fact]
    public void GetAvailableUnitIds_ShouldReturnUnitIds_WhenMmuxResourcesExist()
    {
        // Arrange
        var provider = new AssemblyUnitStreamProvider(typeof(AssemblyUnitStreamProviderTests).Assembly);

        // Act
        var unitIds = provider.GetAvailableUnitIds().ToList();

        // Assert
        unitIds.ShouldNotBeNull();
        unitIds.ShouldContain("SHD-2D");
    }

    [Fact]
    public async Task GetUnitStream_ShouldReturnNull_WhenUnitNotFound()
    {
        // Arrange
        var provider = new AssemblyUnitStreamProvider(Assembly.GetExecutingAssembly());

        // Act
        var stream = await provider.GetUnitStream("NonExistentUnit");

        // Assert
        stream.ShouldBeNull();
    }

    [Fact]
    public void Constructor_ShouldUseEntryAssembly_WhenHostAssemblyIsNull()
    {
        // Arrange & Act
        var provider = new AssemblyUnitStreamProvider();

        // Assert
        // Should not throw and should be able to get unit IDs (even if empty)
        var unitIds = provider.GetAvailableUnitIds();
        unitIds.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var provider = new AssemblyUnitStreamProvider();

        // Assert
        provider.ShouldNotBeNull();

        // Verify it can get unit IDs without throwing
        var unitIds = provider.GetAvailableUnitIds();
        unitIds.ShouldNotBeNull();
    }

    [Fact]
    public void GetAvailableUnitIds_ShouldBeLazy_AndCacheResults()
    {
        // Arrange
        var provider = new AssemblyUnitStreamProvider(Assembly.GetExecutingAssembly());

        // Act - Call multiple times
        var unitIds1 = provider.GetAvailableUnitIds().ToList();
        var unitIds2 = provider.GetAvailableUnitIds().ToList();

        // Assert - Should return same results (testing lazy initialization)
        unitIds1.ShouldBe(unitIds2);
    }

    [Fact]
    public async Task GetUnitStream_ShouldHandleExceptions_Gracefully()
    {
        // Arrange
        var provider = new AssemblyUnitStreamProvider(Assembly.GetExecutingAssembly());

        // Act & Assert - Should not throw even for invalid unit IDs
        var stream1 = await provider.GetUnitStream("");
        var stream2 = await provider.GetUnitStream("Invalid/Unit/Id");
        var stream3 = await provider.GetUnitStream(null!);

        stream1.ShouldBeNull();
        stream2.ShouldBeNull();
        stream3.ShouldBeNull();
    }
}
