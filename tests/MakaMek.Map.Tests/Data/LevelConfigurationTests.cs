using Sanet.MakaMek.Map.Data;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Data;

public class LevelConfigurationTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        // Arrange
        const double hillCoverage = 0.5;
        const int maxElevation = 3;
        const int seed = 42;

        // Act
        var config = new LevelConfiguration(hillCoverage, maxElevation, seed);

        // Assert
        config.HillCoverage.ShouldBe(hillCoverage);
        config.MaxElevation.ShouldBe(maxElevation);
        config.Seed.ShouldBe(seed);
    }
    
    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Constructor_WithInvalidHillCoverage_Throws(double hillCoverage)
    {
        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => new LevelConfiguration(hillCoverage, 1, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithInvalidMaxElevation_Throws(int maxElevation)
    {
        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => new LevelConfiguration(0.5, maxElevation, null));
    }
}