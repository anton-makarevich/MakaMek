using System.Globalization;
using Avalonia.Media;
using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class HexColorToBrushConverterTests
{
    private readonly HexColorToBrushConverter _sut = new();

    [Theory]
    [InlineData("#FF0000", 255, 0, 0)] // Red
    [InlineData("#00FF00", 0, 255, 0)] // Green
    [InlineData("#0000FF", 0, 0, 255)] // Blue
    [InlineData("#FFFFFF", 255, 255, 255)] // White
    public void Convert_ValidHexColor_ReturnsCorrectBrush(string hexColor, byte r, byte g, byte b)
    {
        // Act
        var result = _sut.Convert(hexColor, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.R.ShouldBe(r);
        result.Color.G.ShouldBe(g);
        result.Color.B.ShouldBe(b);
        result.Color.A.ShouldBe((byte)255);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("#XYZ")]
    public void Convert_InvalidInput_ReturnsTransparent(string? invalidInput)
    {
        // Act
        var result = _sut.Convert(invalidInput, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.A.ShouldBe((byte)0);
        result.Color.R.ShouldBe((byte)255);
        result.Color.G.ShouldBe((byte)255);
        result.Color.B.ShouldBe((byte)255);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        // Act & Assert
        Should.Throw<NotSupportedException>(() =>
            _sut.ConvertBack(null, typeof(string), null, CultureInfo.InvariantCulture));
    }
}