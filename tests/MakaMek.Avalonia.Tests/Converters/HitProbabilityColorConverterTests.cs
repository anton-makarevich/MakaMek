using System.Globalization;
using Avalonia.Media;
using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class HitProbabilityColorConverterTests
{
    private readonly HitProbabilityColorConverter _sut = new();

    [Theory]
    [InlineData(0, 128, 128, 128)] // 0% -> Gray
    [InlineData(-1, 128, 128, 128)] // Negative value -> Gray
    [InlineData(10, 255, 0, 0)] // 10% -> Red (below default low threshold)
    [InlineData(40, 255, 165, 0)] // 40% -> Orange (between default thresholds)
    [InlineData(75, 0, 128, 0)] // 75% -> Green (above default medium threshold)
    public void Convert_HitProbability_ReturnsCorrectColor(double probability, byte r, byte g, byte b)
    {
        // Act
        var result = _sut.Convert(probability, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.R.ShouldBe(r);
        result.Color.G.ShouldBe(g);
        result.Color.B.ShouldBe(b);
    }

    [Theory]
    [InlineData(10, "20,50", 255, 0, 0)] // 10% -> Red (below custom low threshold)
    [InlineData(35, "20,50", 255, 165, 0)] // 35% -> Orange (between custom thresholds)
    [InlineData(60, "20,50", 0, 128, 0)] // 60% -> Green (above custom medium threshold)
    public void Convert_WithCustomThresholds_ReturnsCorrectColor(double probability, string thresholds, byte r, byte g, byte b)
    {
        // Act
        var result = _sut.Convert(probability, typeof(IBrush), thresholds, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.R.ShouldBe(r);
        result.Color.G.ShouldBe(g);
        result.Color.B.ShouldBe(b);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a number")]
    [InlineData(true)]
    public void Convert_InvalidInput_ReturnsWhite(object? invalidInput)
    {
        // Act
        var result = _sut.Convert(invalidInput, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.R.ShouldBe((byte)255);
        result.Color.G.ShouldBe((byte)255);
        result.Color.B.ShouldBe((byte)255);
    }

    [Fact]
    public void Convert_InvalidTargetType_ReturnsWhite()
    {
        // Act
        var result = _sut.Convert(50.0, typeof(string), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.R.ShouldBe((byte)255);
        result.Color.G.ShouldBe((byte)255);
        result.Color.B.ShouldBe((byte)255);
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Act & Assert
        Should.Throw<NotImplementedException>(() =>
            _sut.ConvertBack(null, typeof(double), null, CultureInfo.InvariantCulture));
    }
}