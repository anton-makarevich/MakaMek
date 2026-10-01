using System.Globalization;
using NSubstitute;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Localization;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class UnitStatusTextConverterTests
{
    private readonly UnitStatusTextConverter _sut;

    public UnitStatusTextConverterTests()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString(Arg.Any<string>()).Returns(call => call.Arg<string>() switch
        {
            "UnitHud_StatusDestroyed" => "Destroyed",
            "UnitHud_StatusShutdown" => "Shutdown",
            "UnitHud_StatusImmobile" => "Immobile",
            "UnitHud_StatusProne" => "Prone",
            "UnitHud_StatusOperational" => "Operational",
            _ => "Unavailable"
        });
        _sut = new UnitStatusTextConverter(localization);
    }

    private string Convert(object? value) =>
        (string)_sut.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(UnitStatus.Destroyed, "✖ Destroyed")]
    [InlineData(UnitStatus.Shutdown, "⚠ Shutdown")]
    [InlineData(UnitStatus.Immobile, "◆ Immobile")]
    [InlineData(UnitStatus.Prone, "↘ Prone")]
    [InlineData(UnitStatus.Active, "● Operational")]
    [InlineData(UnitStatus.None, "? Unavailable")]
    public void Convert_NamesTheStatusWithItsSymbol(UnitStatus status, string expected)
        => Convert(status).ShouldBe(expected);

    /// <summary>
    /// UnitStatus is a flag set, and a mech can be several of these at once. The card has room for
    /// one line, so the worst state wins rather than the first one the enum happens to declare.
    /// </summary>
    [Theory]
    [InlineData(UnitStatus.Destroyed | UnitStatus.Prone | UnitStatus.Immobile, "✖ Destroyed")]
    [InlineData(UnitStatus.Shutdown | UnitStatus.Prone, "⚠ Shutdown")]
    [InlineData(UnitStatus.Immobile | UnitStatus.Prone, "◆ Immobile")]
    [InlineData(UnitStatus.Prone | UnitStatus.Active, "↘ Prone")]
    public void Convert_ReportsTheMostSevereFlagThatIsSet(UnitStatus status, string expected)
        => Convert(status).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("Active")]
    [InlineData(1)]
    public void Convert_FallsBackToUnavailable_ForAnythingThatIsNotAStatus(object? value)
        => Convert(value).ShouldBe("? Unavailable");

    [Fact]
    public void ConvertBack_IsNotSupported()
        => Should.Throw<NotSupportedException>(() => _sut.ConvertBack(
            "● Operational", typeof(UnitStatus), null, CultureInfo.InvariantCulture));
}
