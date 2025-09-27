using System.Reflection;
using Sanet.MakaMek.Core.Services;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services;

public class AssemblyResourceStreamProviderTests
{
    [Fact]
    public void GetAvailableUnitIds_ShouldReturnUnitIds_WhenMmuxResourcesExist()
    {
        // Arrange
        var provider = new AssemblyResourceStreamProvider("mmux", typeof(AssemblyResourceStreamProviderTests).Assembly);

        // Act
        var unitIds = provider.GetAvailableResourceIds().ToList();

        // Assert
        unitIds.ShouldNotBeNull();
        unitIds.ShouldContain("SHD-2D");
    }

    [Fact]
    public async Task GetUnitStream_ShouldReturnNull_WhenUnitNotFound()
    {
        // Arrange
        var provider = new AssemblyResourceStreamProvider("mmux", Assembly.GetExecutingAssembly());

        // Act
        var stream = await provider.GetResourceStream("NonExistentUnit");

        // Assert
        stream.ShouldBeNull();
    }

    [Fact]
    public void Constructor_ShouldUseEntryAssembly_WhenHostAssemblyIsNull()
    {
        // Arrange & Act
        var provider = new AssemblyResourceStreamProvider("mmux");

        // Assert
        // Should not throw and should be able to get unit IDs (even if empty)
        var unitIds = provider.GetAvailableResourceIds();
        unitIds.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var provider = new AssemblyResourceStreamProvider("mmux");

        // Assert
        provider.ShouldNotBeNull();

        // Verify it can get unit IDs without throwing
        var unitIds = provider.GetAvailableResourceIds();
        unitIds.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenResourceTypeIsNull()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => new AssemblyResourceStreamProvider(null!));
    }

    [Fact]
    public void GetAvailableUnitIds_ShouldBeLazy_AndCacheResults()
    {
        // Arrange
        var provider = new AssemblyResourceStreamProvider("mmux", Assembly.GetExecutingAssembly());

        // Act - Call multiple times
        var unitIds1 = provider.GetAvailableResourceIds().ToList();
        var unitIds2 = provider.GetAvailableResourceIds().ToList();

        // Assert - Should return same results (testing lazy initialization)
        unitIds1.ShouldBe(unitIds2);
    }

    [Fact]
    public async Task GetUnitStream_ShouldHandleExceptions_Gracefully()
    {
        // Arrange
        var provider = new AssemblyResourceStreamProvider("mmux", Assembly.GetExecutingAssembly());

        // Act & Assert - Should not throw even for invalid unit IDs
        var stream1 = await provider.GetResourceStream("");
        var stream2 = await provider.GetResourceStream("Invalid/Unit/Id");
        var stream3 = await provider.GetResourceStream(null!);

        stream1.ShouldBeNull();
        stream2.ShouldBeNull();
        stream3.ShouldBeNull();
    }

    [Fact]
    public void GetAvailableUnitIds_ShouldFilterByResourceType()
    {
        // Arrange
        var provider = new AssemblyResourceStreamProvider("mmux", typeof(AssemblyResourceStreamProviderTests).Assembly);

        // Act
        var unitIds = provider.GetAvailableResourceIds().ToList();

        // Assert - Should only contain MMUX resources, not other types
        unitIds.ShouldNotBeNull();
        unitIds.ShouldContain("SHD-2D");
        // Should not contain any non-MMUX resources
        unitIds.ShouldNotContain(id => id.Contains(".") || id.Contains("/") || id.Contains("\\"));
    }

    [Fact]
    public void Constructor_WithDifferentResourceType_ShouldWork()
    {
        // Arrange & Act
        var provider = new AssemblyResourceStreamProvider("json", typeof(AssemblyResourceStreamProviderTests).Assembly);

        // Assert
        provider.ShouldNotBeNull();
        var unitIds = provider.GetAvailableResourceIds();
        unitIds.ShouldNotBeNull();
        // Should not throw even if no JSON resources exist
    }
}
