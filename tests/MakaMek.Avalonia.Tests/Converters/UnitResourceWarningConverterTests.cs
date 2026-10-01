using System.Globalization;
using NSubstitute;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class UnitResourceWarningConverterTests
{
    private static ILocalizationService Localization()
    {
        var service = Substitute.For<ILocalizationService>();
        service.GetString(Arg.Any<string>()).Returns(call => call.Arg<string>());
        return service;
    }

    private sealed class TestWeapon(bool requiresAmmo)
        : Weapon(new WeaponDefinition(
            "Test Weapon", 5, 3,
            new WeaponRange(0, 3, 6, 9),
            WeaponType.Energy, 10, null, 1, 1, 1, 1,
            MakaMekComponent.MachineGun,
            requiresAmmo ? MakaMekComponent.ISAmmoMG : null));

    /// <summary>
    /// Builds a unit whose weapon inventory and ammunition state match the scenario under test.
    /// </summary>
    /// <param name="totalWeapons">Weapons mounted on the unit.</param>
    /// <param name="availableWeapons">Weapons still usable; the difference is treated as damaged.</param>
    /// <param name="remainingShots">Shots reported for every ammo-using weapon.</param>
    private static IUnit UnitWith(int totalWeapons, int availableWeapons, int remainingShots, bool requiresAmmo = true)
    {
        var all = Enumerable.Range(0, totalWeapons).Select(_ => new TestWeapon(requiresAmmo)).ToList();
        var available = all.Take(availableWeapons).ToList();

        var unit = Substitute.For<IUnit>();
        unit.GetAllComponents<Weapon>().Returns(all);
        unit.GetAvailableComponents<Weapon>().Returns(available);
        unit.GetRemainingAmmoShots(Arg.Any<Weapon>()).Returns(remainingShots);
        return unit;
    }

    private static object Convert(object? value)
        => new UnitResourceWarningConverter(Localization())
            .Convert(value, typeof(string), null, CultureInfo.InvariantCulture);

    [Fact]
    public void Convert_WhenWeaponsDamagedAndAmmoEmpty_ReportsBothProblems()
        => Convert(UnitWith(totalWeapons: 3, availableWeapons: 2, remainingShots: 0))
            .ShouldBe("UnitHud_WarningsDamagedAndAmmoEmpty");

    [Fact]
    public void Convert_WhenWeaponsDamagedAndAmmoRemains_ReportsOnlyTheDamage()
        => Convert(UnitWith(totalWeapons: 3, availableWeapons: 2, remainingShots: 10))
            .ShouldBe("UnitHud_WarningsWeaponsDamaged");

    [Fact]
    public void Convert_WhenAllWeaponsUsableButAmmoEmpty_ReportsEmptyAmmo()
        => Convert(UnitWith(totalWeapons: 2, availableWeapons: 2, remainingShots: 0))
            .ShouldBe("UnitHud_WarningsAmmoEmpty");

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Convert_WhenAmmoIsAtOrBelowTwoShots_ReportsLowAmmo(int remainingShots)
        => Convert(UnitWith(totalWeapons: 2, availableWeapons: 2, remainingShots: remainingShots))
            .ShouldBe("UnitHud_WarningsAmmoLow");

    [Fact]
    public void Convert_WhenAmmoIsAboveTheLowThreshold_ReportsNothing()
        => Convert(UnitWith(totalWeapons: 2, availableWeapons: 2, remainingShots: 3))
            .ShouldBe(string.Empty);

    [Fact]
    public void Convert_WhenNoWeaponUsesAmmo_IgnoresTheShotCount()
        => Convert(UnitWith(totalWeapons: 2, availableWeapons: 2, remainingShots: 0, requiresAmmo: false))
            .ShouldBe(string.Empty);

    [Fact]
    public void Convert_WhenUnitHasNoWeapons_ReportsNothing()
        => Convert(UnitWith(totalWeapons: 0, availableWeapons: 0, remainingShots: 0))
            .ShouldBe(string.Empty);

    [Theory]
    [InlineData(null)]
    [InlineData("not a unit")]
    public void Convert_WhenValueIsNotAUnit_ReportsNothing(object? value)
        => Convert(value).ShouldBe(string.Empty);

    [Fact]
    public void ConvertBack_IsNotSupported()
        => Should.Throw<NotSupportedException>(() => new UnitResourceWarningConverter(Localization())
            .ConvertBack("warning", typeof(IUnit), null, CultureInfo.InvariantCulture));
}
