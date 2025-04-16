using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;
using System.Globalization;

namespace MakaMek.Avalonia.Tests.Converters;

public class StringNotEmptyToBoolConverterTests
{
    private readonly StringNotEmptyToBoolConverter _sut = StringNotEmptyToBoolConverter.Instance;

    [Fact]
    public void Convert_WithNonEmptyString_ReturnsTrue()
    {
        // Arrange
        var value = "Some text";

        // Act
        var result = _sut.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<bool>();
        result.ShouldBe(true);
    }

    [Fact]
    public void Convert_WithEmptyString_ReturnsFalse()
    {
        // Arrange
        var value = string.Empty;

        // Act
        var result = _sut.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<bool>();
        result.ShouldBe(false);
    }

    [Fact]
    public void Convert_WithNullString_ReturnsFalse()
    {
        // Arrange
        string? value = null;

        // Act
        var result = _sut.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<bool>();
        result.ShouldBe(false);
    }

    [Fact]
    public void Convert_WithNonStringValue_ReturnsFalse()
    {
        // Arrange
        var value = 123;

        // Act
        var result = _sut.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<bool>();
        result.ShouldBe(false);
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Act & Assert
        Should.Throw<System.NotImplementedException>(() =>
            _sut.ConvertBack(true, typeof(string), null, CultureInfo.InvariantCulture));
    }
}