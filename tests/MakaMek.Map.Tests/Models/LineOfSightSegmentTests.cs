using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

public class LineOfSightSegmentTests
{
    [Fact]
    public void Equals_OnlyMainOptions_ShouldBeEqual()
    {
        // Arrange
        var hex = new HexCoordinates(3, 4);
        var segment1 = new LineOfSightSegment(hex);
        var segment2 = new LineOfSightSegment(hex);

        // Act & Assert
        segment1.ShouldBe(segment2);
        segment1.GetHashCode().ShouldBe(segment2.GetHashCode());
    }

    [Fact]
    public void Equals_OnlyMainOptions_ShouldNotBeEqual()
    {
        // Arrange
        var segment1 = new LineOfSightSegment(new HexCoordinates(3, 4));
        var segment2 = new LineOfSightSegment(new HexCoordinates(3, 5));

        // Act & Assert
        segment1.ShouldNotBe(segment2);
        segment1.GetHashCode().ShouldNotBe(segment2.GetHashCode());
    }

    [Fact]
    public void Equals_WithSecondOptions_SameOrder_ShouldBeEqual()
    {
        // Arrange
        var main = new HexCoordinates(3, 4);
        var second = new HexCoordinates(3, 5);
        var segment1 = new LineOfSightSegment(main, second);
        var segment2 = new LineOfSightSegment(main, second);

        // Act & Assert
        segment1.ShouldBe(segment2);
        segment1.GetHashCode().ShouldBe(segment2.GetHashCode());
    }

    [Fact]
    public void Equals_OneWithSecondOption_ShouldNotBeEqual()
    {
        // Arrange
        var hex1 = new HexCoordinates(3, 4);
        var hex2 = new HexCoordinates(3, 5);
        var segment1 = new LineOfSightSegment(hex1);
        var segment2 = new LineOfSightSegment(hex1, hex2);

        // Act & Assert
        segment1.ShouldNotBe(segment2);
        segment2.ShouldNotBe(segment1);
        segment1.GetHashCode().ShouldNotBe(segment2.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentSecondOptions_ShouldNotBeEqual()
    {
        // Arrange
        var hex1 = new HexCoordinates(3, 4);
        var hex2 = new HexCoordinates(3, 5);
        var hex3 = new HexCoordinates(3, 6);
        var segment1 = new LineOfSightSegment(hex1, hex2);
        var segment2 = new LineOfSightSegment(hex1, hex3);

        // Act & Assert
        segment1.ShouldNotBe(segment2);
        segment1.GetHashCode().ShouldNotBe(segment2.GetHashCode());
    }
}
