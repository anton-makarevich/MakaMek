using System.Globalization;
using NSubstitute;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Localization;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class UnitActionHintConverterTests
{
    private static ILocalizationService Localization()
    {
        var service = Substitute.For<ILocalizationService>();
        service.GetString(Arg.Any<string>()).Returns(call => call.Arg<string>() switch
        {
            "UnitHud_ActionWeaponsOnline" => "Weapons online",
            "UnitHud_ActionWeaponsUnavailable" => "Weapons unavailable",
            _ => "Unavailable"
        });
        return service;
    }

    /// <summary>
    /// The states the status label already names come back empty, because the card showed them
    /// twice otherwise. Only weapon readiness is this converter's to report.
    /// </summary>
    [Theory]
    [InlineData(UnitStatus.Destroyed, false, "")]
    [InlineData(UnitStatus.Shutdown, true, "")]
    [InlineData(UnitStatus.Immobile, true, "")]
    [InlineData(UnitStatus.Active, true, "Weapons online")]
    [InlineData(UnitStatus.Active, false, "Weapons unavailable")]
    public void Convert_ReportsOnlyWhatTheStatusLabelDoesNot(UnitStatus status, bool canFire, string expected)
    {
        var unit = Substitute.For<IUnit>();
        unit.Status.Returns(status);
        unit.IsDestroyed.Returns(status.HasFlag(UnitStatus.Destroyed));
        unit.IsShutdown.Returns(status.HasFlag(UnitStatus.Shutdown));
        unit.IsImmobile.Returns(status.HasFlag(UnitStatus.Immobile));
        unit.CanFireWeapons.Returns(canFire);

        var result = new UnitActionHintConverter(Localization()).Convert(unit, typeof(string), null, CultureInfo.InvariantCulture);

        result.ShouldBe(expected);
    }

    [Fact]
    public void Convert_ReturnsEmpty_ForSomethingThatIsNotAUnit()
        => new UnitActionHintConverter(Localization())
            .Convert("not a unit", typeof(string), null, CultureInfo.InvariantCulture)
            .ShouldBe(string.Empty);

    [Fact]
    public void ConvertBack_IsNotSupported()
        => Should.Throw<NotSupportedException>(() => new UnitActionHintConverter(Localization())
            .ConvertBack("hint", typeof(IUnit), null, CultureInfo.InvariantCulture));
}
