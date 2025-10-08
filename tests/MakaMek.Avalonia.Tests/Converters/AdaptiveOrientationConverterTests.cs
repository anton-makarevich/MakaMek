using System.Globalization;
using Avalonia;
using Avalonia.Layout;
using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class AdaptiveOrientationConverterTests
{
    [Fact]
    public void Convert_WithNullValue_ReturnsVerticalOrientation()
    {
        // Arrange
        var sut = new AdaptiveOrientationConverter();

        // Act
        var result = sut.Convert(null, typeof(Orientation), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(Orientation.Vertical);
    }
    
    [Fact]
    public void Convert_WithNonSizeValue_ReturnsVerticalOrientation()
    {
        // Arrange
        var sut = new AdaptiveOrientationConverter();

        // Act
        var result = sut.Convert("not a size", typeof(Orientation), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(Orientation.Vertical);
    }
    
    [Fact]
    public void Convert_WithHorizontalSize_ReturnsHorizontalOrientation()
    {
        // Arrange
        var sut = new AdaptiveOrientationConverter();

        // Act
        var result = sut.Convert(new Size(100, 50), typeof(Orientation), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(Orientation.Horizontal);
    }
    
    [Fact]
    public void Convert_WithVerticalSize_ReturnsVerticalOrientation()
    {
        // Arrange
        var sut = new AdaptiveOrientationConverter();

        // Act
        var result = sut.Convert(new Size(50, 100), typeof(Orientation), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(Orientation.Vertical);
    }
    
    [Fact]
    public void Convert_WithSquareSize_ReturnsVerticalOrientation()
    {
        // Arrange
        var sut = new AdaptiveOrientationConverter();

        // Act
        var result = sut.Convert(new Size(100, 100), typeof(Orientation), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(Orientation.Vertical);
    }
    
    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        // Arrange
        var sut = new AdaptiveOrientationConverter();

        // Act & Assert
        Should.Throw<NotSupportedException>(() =>
            sut.ConvertBack(Orientation.Horizontal, typeof(Size), null, CultureInfo.InvariantCulture));
    }
}