using System.Globalization;
using Avalonia.Media;
using NSubstitute;
using Sanet.MakaMek.Avalonia.Controls.Services;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Core.Models.Units;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class UnitStatusToBrushConverterTests
{
    private readonly IAvaloniaResourcesLocator _resourcesLocator = Substitute.For<IAvaloniaResourcesLocator>();
    private readonly UnitStatusToBrushConverter _sut;

    public UnitStatusToBrushConverterTests()
    {
        _sut = new UnitStatusToBrushConverter(_resourcesLocator);
    }

    [Theory]
    [InlineData(UnitStatus.Destroyed, "ErrorBrush")]
    [InlineData(UnitStatus.Shutdown, "WarningBrush")]
    [InlineData(UnitStatus.Immobile, "DamagedBrush")]
    [InlineData(UnitStatus.Prone, "DamagedBrush")]
    [InlineData(UnitStatus.Active, "SuccessBrush")]
    [InlineData(UnitStatus.None, "BorderBrush")]
    public void Convert_ResolvesTheThemedBrushForTheStatus(UnitStatus status, string expectedKey)
    {
        var brush = new SolidColorBrush(Colors.Fuchsia);
        _resourcesLocator.TryFindResource(expectedKey).Returns(brush);

        var result = _sut.Convert(status, typeof(IBrush), null, CultureInfo.InvariantCulture);

        result.ShouldBe(brush);
    }

    [Fact]
    public void Convert_DestroyedWins_WhenSeveralFlagsAreSet()
    {
        var brush = new SolidColorBrush(Colors.Fuchsia);
        _resourcesLocator.TryFindResource("ErrorBrush").Returns(brush);

        var result = _sut.Convert(UnitStatus.Destroyed | UnitStatus.Prone, typeof(IBrush), null, CultureInfo.InvariantCulture);

        result.ShouldBe(brush);
    }

    [Fact]
    public void Convert_FallsBackToGray_WhenTheResourceIsMissing()
    {
        _resourcesLocator.TryFindResource(Arg.Any<string>()).Returns((object?)null);

        var result = _sut.Convert(UnitStatus.Active, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Gray);
    }

    [Fact]
    public void Convert_TreatsAnUnexpectedValueAsNoStatus()
    {
        var brush = new SolidColorBrush(Colors.Fuchsia);
        _resourcesLocator.TryFindResource("BorderBrush").Returns(brush);

        var result = _sut.Convert("not a status", typeof(IBrush), null, CultureInfo.InvariantCulture);

        result.ShouldBe(brush);
    }

    [Fact]
    public void ConvertBack_IsNotSupported()
    {
        Should.Throw<NotSupportedException>(() =>
            _sut.ConvertBack(null, typeof(UnitStatus), null, CultureInfo.InvariantCulture));
    }
}
