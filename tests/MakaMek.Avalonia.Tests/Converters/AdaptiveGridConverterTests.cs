using System.Globalization;
using Avalonia;
using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class AdaptiveGridConverterTests
{
    [Fact]
    public void Convert_WithNullValue_ReturnsDefaultColumns()
    {
        // Arrange
        var sut = new AdaptiveGridConverter();

        // Act
        var result = sut.Convert(null, typeof(int), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(1);
    }
    
    [Fact]
    public void Convert_WithNonSizeValue_ReturnsDefaultColumns()
    {
        // Arrange
        var sut = new AdaptiveGridConverter();

        // Act
        var result = sut.Convert("not a size", typeof(int), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(1);
    }
    
    [Fact]
    public void Convert_WithHorizontalSize_ReturnsTwoColumns()
    {
        // Arrange
        var sut = new AdaptiveGridConverter();

        // Act
        var result = sut.Convert(new Size(100, 50), typeof(int), GridOrientation.Columns, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(2);
    }
    
    [Fact]
    public void Convert_WithVerticalSize_ReturnsTwoRows()
    {
        // Arrange
        var sut = new AdaptiveGridConverter();

        // Act
        var result = sut.Convert(new Size(50, 100), typeof(int), GridOrientation.Rows, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(2);
    }
    
    [Fact]
    public void Convert_WithHorizontalSize_ReturnsOneRow()
    {
        // Arrange
        var sut = new AdaptiveGridConverter();

        // Act
        var result = sut.Convert(new Size(100, 50), typeof(int), GridOrientation.Rows, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(1);
    }
    
    [Fact]
    public void Convert_WithVerticalSize_ReturnsOneColumn()
    {
        // Arrange
        var sut = new AdaptiveGridConverter();

        // Act
        var result = sut.Convert(new Size(50, 100), typeof(int), GridOrientation.Columns, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(1);
    }
    
    [Fact]
    public void Convert_WithSquareSize_ReturnsDefaultColumns()
    {
        // Arrange
        var sut = new AdaptiveGridConverter();

        // Act
        var result = sut.Convert(new Size(100, 100), typeof(int), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(1);
    }
    
    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        // Arrange
        var sut = new AdaptiveGridConverter();

        // Act & Assert
        Should.Throw<NotSupportedException>(() =>
            sut.ConvertBack(1, typeof(Size), null, CultureInfo.InvariantCulture));
    }
}