using System.Reflection;
using Sanet.MakaMek.Core.Services;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services;

public class AssemblyResourceStreamProviderTests
{
    [Fact]
    public async Task GetAvailableUnitIds_ShouldReturnUnitIds_WhenMmuxResourcesExist()
    {
        // Arrange
        var sut = new AssemblyResourceStreamProvider("mmux", typeof(AssemblyResourceStreamProviderTests).Assembly);

        // Act
        var unitIds = (await sut.GetAvailableResourceIds()).ToList();

        // Assert
        unitIds.ShouldNotBeNull();
        unitIds.ShouldHaveSingleItem();
        unitIds[0].ShouldContain("SHD-2D");
    }
    
    [Fact]
    public async Task GetResourceStream_ShouldReturnData_WhenMmuxResourcesExist()
    {
        // Arrange
        var sut = new AssemblyResourceStreamProvider("mmux", typeof(AssemblyResourceStreamProviderTests).Assembly);
        var unitId = (await sut.GetAvailableResourceIds()).ToList().First();
        unitId.ShouldNotBeNullOrEmpty();
        
        var stream = await sut.GetResourceStream(unitId);
        stream.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetResourceStream_ShouldReturnNull_WhenUnitNotFound()
    {
        // Arrange
        var sut = new AssemblyResourceStreamProvider("mmux", Assembly.GetExecutingAssembly());

        // Act
        var stream = await sut.GetResourceStream("NonExistentUnit");

        // Assert
        stream.ShouldBeNull();
    }

    [Fact]
    public void Constructor_ShouldUseEntryAssembly_WhenHostAssemblyIsNull()
    {
        // Arrange & Act
        var sut = new AssemblyResourceStreamProvider("mmux");

        // Assert
        // Should not throw and should be able to get unit IDs (even if empty)
        var unitIds = sut.GetAvailableResourceIds();
        unitIds.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var sut = new AssemblyResourceStreamProvider("mmux");

        // Assert
        sut.ShouldNotBeNull();

        // Verify it can get unit IDs without throwing
        var unitIds = sut.GetAvailableResourceIds();
        unitIds.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenResourceTypeIsNull()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => new AssemblyResourceStreamProvider(null!));
    }

    [Fact]
    public async Task GetAvailableUnitIds_ShouldBeLazy_AndCacheResults()
    {
        // Arrange
        var sut = new AssemblyResourceStreamProvider("mmux", Assembly.GetExecutingAssembly());

        // Act - Call multiple times
        var unitIds1 = (await sut.GetAvailableResourceIds()).ToList();
        var unitIds2 = (await sut.GetAvailableResourceIds()).ToList();

        // Assert - Should return same results (testing lazy initialization)
        unitIds1.ShouldBe(unitIds2);
    }

    [Fact]
    public async Task GetResourceStream_ShouldHandleExceptions_Gracefully()
    {
        // Arrange
        var sut = new AssemblyResourceStreamProvider("mmux", Assembly.GetExecutingAssembly());

        // Act & Assert - Should not throw even for invalid unit IDs
        var stream1 = await sut.GetResourceStream("");
        var stream2 = await sut.GetResourceStream("Invalid/Unit/Id");
        var stream3 = await sut.GetResourceStream(null!);

        stream1.ShouldBeNull();
        stream2.ShouldBeNull();
        stream3.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithDifferentResourceType_ShouldWork()
    {
        // Arrange & Act
        var sut = new AssemblyResourceStreamProvider("json", typeof(AssemblyResourceStreamProviderTests).Assembly);

        // Assert
        sut.ShouldNotBeNull();
        var unitIds = sut.GetAvailableResourceIds();
        unitIds.ShouldNotBeNull();
        // Should not throw even if no JSON resources exist
    }
}
