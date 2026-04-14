using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

public class CanonicalBitmaskResultTests
{
    [Fact]
    public void Constructor_ValidValues_SetsPropertiesCorrectly()
    {
        // Arrange
        const byte canonicalMask = 0b001011;
        const int rotationSteps = 3;

        // Act
        var result = new CanonicalBitmaskResult(canonicalMask, rotationSteps);

        // Assert
        result.CanonicalMask.ShouldBe(canonicalMask);
        result.RotationSteps.ShouldBe(rotationSteps);
    }

    [Fact]
    public void Constructor_ZeroMaskAndZeroSteps_SetsPropertiesCorrectly()
    {
        // Arrange
        const byte canonicalMask = 0;
        const int rotationSteps = 0;

        // Act
        var result = new CanonicalBitmaskResult(canonicalMask, rotationSteps);

        // Assert
        result.CanonicalMask.ShouldBe(canonicalMask);
        result.RotationSteps.ShouldBe(rotationSteps);
    }

    [Fact]
    public void Constructor_MaxValidValues_SetsPropertiesCorrectly()
    {
        // Arrange
        const byte canonicalMask = 0b111111; // 63
        const int rotationSteps = 5;

        // Act
        var result = new CanonicalBitmaskResult(canonicalMask, rotationSteps);

        // Assert
        result.CanonicalMask.ShouldBe(canonicalMask);
        result.RotationSteps.ShouldBe(rotationSteps);
    }

    [Theory]
    [InlineData(0b1000_0000)] // 128 - bit 7 set
    [InlineData(0b1100_0000)] // 192 - bits 7 and 6 set
    [InlineData(0b1111_1111)] // 255 - all bits set
    [InlineData(0b0100_0000)] // 64 - bit 6 set
    public void Constructor_CanonicalMaskWithBits6Or7Set_ThrowsArgumentOutOfRangeException(byte invalidMask)
    {
        // Arrange & Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new CanonicalBitmaskResult(invalidMask, 0));

        // Assert
        exception.ParamName.ShouldBe("canonicalMask");
        exception.Message.ShouldContain("Canonical mask must be a 6-bit value (0-63).");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    [InlineData(-100)]
    public void Constructor_RotationStepsNegative_ThrowsArgumentOutOfRangeException(int invalidSteps)
    {
        // Arrange & Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new CanonicalBitmaskResult(0, invalidSteps));

        // Assert
        exception.ParamName.ShouldBe("rotationSteps");
        exception.Message.ShouldContain("Rotation steps must be in range 0..5.");
    }

    [Theory]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(100)]
    public void Constructor_RotationStepsGreaterThan5_ThrowsArgumentOutOfRangeException(int invalidSteps)
    {
        // Arrange & Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new CanonicalBitmaskResult(0, invalidSteps));

        // Assert
        exception.ParamName.ShouldBe("rotationSteps");
        exception.Message.ShouldContain("Rotation steps must be in range 0..5.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void Constructor_RotationStepsAtValidBoundaries_DoesNotThrow(int validSteps)
    {
        // Arrange
        const byte canonicalMask = 0b000001;

        // Act & Assert
        Should.NotThrow(() => new CanonicalBitmaskResult(canonicalMask, validSteps));
    }
}
