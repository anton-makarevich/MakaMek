using System.Globalization;
using Avalonia.Media;
using NSubstitute;
using Sanet.MakaMek.Avalonia.Controls.Services;
using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class HeatRiskToBrushConverterTests
{
    private readonly IAvaloniaResourcesLocator _resourcesLocator = Substitute.For<IAvaloniaResourcesLocator>();
    private readonly HeatRiskToBrushConverter _sut;

    public HeatRiskToBrushConverterTests()
    {
        _sut = new HeatRiskToBrushConverter(_resourcesLocator);
    }

    [Theory]
    [InlineData(0, "SuccessBrush")]
    [InlineData(4, "SuccessBrush")]
    [InlineData(5, "DamagedBrush")]
    [InlineData(9, "DamagedBrush")]
    [InlineData(10, "WarningBrush")]
    [InlineData(19, "WarningBrush")]
    [InlineData(20, "ErrorBrush")]
    [InlineData(40, "ErrorBrush")]
    public void Convert_ResolvesTheThemedBrushForTheHeatBand(int heat, string expectedKey)
    {
        var brush = new SolidColorBrush(Colors.Fuchsia);
        _resourcesLocator.TryFindResource(expectedKey).Returns(brush);

        var result = _sut.Convert(heat, typeof(IBrush), null, CultureInfo.InvariantCulture);

        result.ShouldBe(brush);
    }

    [Fact]
    public void Convert_TreatsAnUnexpectedValueAsNoHeat()
    {
        var brush = new SolidColorBrush(Colors.Fuchsia);
        _resourcesLocator.TryFindResource("SuccessBrush").Returns(brush);

        var result = _sut.Convert(null, typeof(IBrush), null, CultureInfo.InvariantCulture);

        result.ShouldBe(brush);
    }

    [Fact]
    public void Convert_FallsBackToGray_WhenTheResourceIsMissing()
    {
        _resourcesLocator.TryFindResource(Arg.Any<string>()).Returns((object?)null);

        var result = _sut.Convert(25, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Gray);
    }

    [Fact]
    public void ConvertBack_IsNotSupported()
    {
        Should.Throw<NotSupportedException>(() =>
            _sut.ConvertBack(null, typeof(int), null, CultureInfo.InvariantCulture));
    }
}
