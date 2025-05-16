using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;
using System.Globalization;

namespace MakaMek.Avalonia.Tests.Converters;

public class GreaterThanValueConverterTests
{
    private readonly GreaterThanValueConverter _sut = new();

    [Fact]
    public void Convert_WithValueGreaterThanDefault_ReturnsTrue()
    {
        // Arrange
        var value = 2;

        // Act
        var result = _sut.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<bool>();
        result.ShouldBe(true);
    }

    [Fact]
    public void Convert_WithValueEqualToDefault_ReturnsFalse()
    {
        // Arrange
        var value = 1;

        // Act
        var result = _sut.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<bool>();
        result.ShouldBe(false);
    }

    [Fact]
    public void Convert_WithValueLessThanDefault_ReturnsFalse()
    {
        // Arrange
        var value = 0;

        // Act
        var result = _sut.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<bool>();
        result.ShouldBe(false);
    }

    [Fact]
    public void Convert_WithCustomParameter_ComparesAgainstCustomValue()
    {
        // Arrange
        var value = 5;
        var parameter = "3";

        // Act
        var result = _sut.Convert(value, typeof(bool), parameter, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<bool>();
        result.ShouldBe(true);
    }

    [Fact]
    public void Convert_WithValueEqualToCustomParameter_ReturnsFalse()
    {
        // Arrange
        var value = 3;
        var parameter = "3";

        // Act
        var result = _sut.Convert(value, typeof(bool), parameter, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<bool>();
        result.ShouldBe(false);
    }

    [Fact]
    public void Convert_WithValueLessThanCustomParameter_ReturnsFalse()
    {
        // Arrange
        var value = 2;
        var parameter = "3";

        // Act
        var result = _sut.Convert(value, typeof(bool), parameter, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<bool>();
        result.ShouldBe(false);
    }

    [Fact]
    public void Convert_WithNonIntegerValue_ReturnsFalse()
    {
        // Arrange
        var value = "not an integer";

        // Act
        var result = _sut.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<bool>();
        result.ShouldBe(false);
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
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Act & Assert
        Should.Throw<System.NotImplementedException>(() =>
            _sut.ConvertBack(true, typeof(int), null, CultureInfo.InvariantCulture));
    }
}
