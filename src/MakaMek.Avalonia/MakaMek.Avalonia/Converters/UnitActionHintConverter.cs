using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts a unit's current availability into a compact tactical hint for the squad HUD.
/// </summary>
public sealed class UnitActionHintConverter : IValueConverter
{
    private readonly ILocalizationService _localizationService;

    /// <summary>
    /// Creates a converter that resolves tactical action labels through localization.
    /// </summary>
    /// <param name="localizationService">Service used to resolve HUD labels.</param>
    public UnitActionHintConverter(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IUnit unit) return _localizationService.GetString("UnitHud_ActionUnavailable");
        if (unit.IsDestroyed) return _localizationService.GetString("UnitHud_ActionOutOfAction");
        if (unit.IsShutdown) return _localizationService.GetString("UnitHud_ActionShutdown");
        if (unit.IsImmobile) return _localizationService.GetString("UnitHud_ActionImmobile");
        return _localizationService.GetString(unit.CanFireWeapons
            ? "UnitHud_ActionWeaponsOnline"
            : "UnitHud_ActionWeaponsUnavailable");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
