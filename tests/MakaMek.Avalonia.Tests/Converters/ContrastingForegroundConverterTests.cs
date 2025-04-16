using System.Globalization;
using Avalonia.Media;
using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class ContrastingForegroundConverterTests
{
    private readonly ContrastingForegroundConverter _sut = new();

    [Theory]
    [InlineData("#FFFFFF", 0, 0, 0)] // White background -> Black text
    [InlineData("#000000", 255, 255, 255)] // Black background -> White text
    [InlineData("#FF0000", 255, 255, 255)] // Red background -> White text
    [InlineData("#FFFF00", 0, 0, 0)] // Yellow background -> Black text
    public void Convert_ValidHexColor_ReturnsCorrectContrastingColor(string hexColor, byte r, byte g, byte b)
    {
        // Act
        var result = _sut.Convert(hexColor, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.R.ShouldBe(r);
        result.Color.G.ShouldBe(g);
        result.Color.B.ShouldBe(b);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("#XYZ")]
    public void Convert_InvalidInput_ReturnsBlack(string? invalidInput)
    {
        // Act
        var result = _sut.Convert(invalidInput, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.R.ShouldBe((byte)0);
        result.Color.G.ShouldBe((byte)0);
        result.Color.B.ShouldBe((byte)0);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        // Act & Assert
        Should.Throw<NotSupportedException>(() =>
            _sut.ConvertBack(null, typeof(string), null, CultureInfo.InvariantCulture));
    }
}