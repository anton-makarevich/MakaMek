using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;
using System.Globalization;

namespace MakaMek.Avalonia.Tests.Converters;

public class NullToVisibilityConverterTests
{
    private readonly NullToVisibilityConverter _sut = new();

    [Fact]
    public void Convert_WithNonNullValue_ReturnsTrue()
    {
        // Arrange
        var value = new object();

        // Act
        var result = _sut.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<bool>();
        result.ShouldBe(true);
    }

    [Fact]
    public void Convert_WithNullValue_ReturnsFalse()
    {
        // Arrange
        object? value = null;

        // Act
        var result = _sut.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<bool>();
        result.ShouldBe(false);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        // Arrange
        object? value = null;

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => 
            _sut.ConvertBack(value, typeof(object), null, CultureInfo.InvariantCulture));
    }
}
