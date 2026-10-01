using System;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Summarizes ammunition and weapon-damage risks for a compact tactical HUD.
/// </summary>
public sealed class UnitResourceWarningConverter : IValueConverter
{
    private readonly ILocalizationService _localizationService;

    /// <summary>
    /// Creates a converter that resolves resource warning text through localization.
    /// </summary>
    /// <param name="localizationService">Service used to resolve warning labels.</param>
    public UnitResourceWarningConverter(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IUnit unit) return string.Empty;

        var allWeapons = unit.GetAllComponents<Weapon>().ToList();
        var availableWeapons = unit.GetAvailableComponents<Weapon>().ToList();
        var damagedWeapons = allWeapons.Count - availableWeapons.Count;
        var ammoEmpty = availableWeapons.Any(weapon => weapon.RequiresAmmo && unit.GetRemainingAmmoShots(weapon) == 0);
        var ammoLow = availableWeapons.Any(weapon => weapon.RequiresAmmo && unit.GetRemainingAmmoShots(weapon) is > 0 and <= 2);

        return (damagedWeapons > 0, ammoEmpty, ammoLow) switch
        {
            (true, true, _) => _localizationService.GetString("UnitHud_WarningsDamagedAndAmmoEmpty"),
            (true, false, _) => _localizationService.GetString("UnitHud_WarningsWeaponsDamaged"),
            (false, true, _) => _localizationService.GetString("UnitHud_WarningsAmmoEmpty"),
            (false, false, true) => _localizationService.GetString("UnitHud_WarningsAmmoLow"),
            _ => string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
