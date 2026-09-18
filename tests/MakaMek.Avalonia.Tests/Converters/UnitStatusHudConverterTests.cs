using System.Globalization;
using Avalonia.Media;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Localization;
using NSubstitute;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class UnitStatusHudConverterTests
{
    private static ILocalizationService Localization()
    {
        var service = Substitute.For<ILocalizationService>();
        service.GetString(Arg.Any<string>()).Returns(call => call.Arg<string>() switch
        {
            "UnitHud_StatusOperational" => "Operational",
            "UnitHud_StatusProne" => "Prone",
            "UnitHud_StatusImmobile" => "Immobile",
            "UnitHud_StatusShutdown" => "Shutdown",
            "UnitHud_StatusDestroyed" => "Destroyed",
            "UnitHud_StatusUnavailable" => "Unavailable",
            _ => call.Arg<string>()
        });
        return service;
    }

    [Theory]
    [InlineData(UnitStatus.Active, "● Operational")]
    [InlineData(UnitStatus.Prone, "↘ Prone")]
    [InlineData(UnitStatus.Immobile, "◆ Immobile")]
    [InlineData(UnitStatus.Shutdown, "⚠ Shutdown")]
    [InlineData(UnitStatus.Destroyed, "✖ Destroyed")]
    public void StatusTextConverter_ReturnsMostImportantState(UnitStatus status, string expected)
    {
        var result = new UnitStatusTextConverter(Localization()).Convert(status, typeof(string), null, CultureInfo.InvariantCulture);

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, "#ff2e7d32")]
    [InlineData(5, "#ffa65e00")]
    [InlineData(10, "#ffc77700")]
    [InlineData(20, "#ffb3261e")]
    public void HeatRiskConverter_UsesProgressiveWarningColors(int heat, string expectedHex)
    {
        var result = new HeatRiskToBrushConverter().Convert(heat, typeof(IBrush), null, CultureInfo.InvariantCulture);

        result.ShouldBeOfType<SolidColorBrush>().Color.ToString().ShouldBe(expectedHex);
    }

    [Theory]
    [InlineData(25, 50, "50%")]
    [InlineData(1, 3, "33%")]
    [InlineData(0, 0, "0%")]
    [InlineData(60, 50, "100%")]
    public void IntegrityConverter_FormatsClampedPercentage(int current, int maximum, string expected)
    {
        var result = new UnitIntegrityPercentConverter().Convert(
            [current, maximum], typeof(string), null, CultureInfo.InvariantCulture);

        result.ShouldBe(expected);
    }
}
