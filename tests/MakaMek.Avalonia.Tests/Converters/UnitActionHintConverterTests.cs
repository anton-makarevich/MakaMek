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
            "UnitHud_ActionOutOfAction" => "Out of action",
            "UnitHud_ActionShutdown" => "Shutdown",
            "UnitHud_ActionImmobile" => "Immobile",
            "UnitHud_ActionWeaponsOnline" => "Weapons online",
            "UnitHud_ActionWeaponsUnavailable" => "Weapons unavailable",
            _ => "Unavailable"
        });
        return service;
    }

    [Theory]
    [InlineData(UnitStatus.Destroyed, false, "Out of action")]
    [InlineData(UnitStatus.Shutdown, true, "Shutdown")]
    [InlineData(UnitStatus.Immobile, true, "Immobile")]
    [InlineData(UnitStatus.Active, true, "Weapons online")]
    [InlineData(UnitStatus.Active, false, "Weapons unavailable")]
    public void Convert_ReturnsHighestPriorityTacticalHint(UnitStatus status, bool canFire, string expected)
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
}
