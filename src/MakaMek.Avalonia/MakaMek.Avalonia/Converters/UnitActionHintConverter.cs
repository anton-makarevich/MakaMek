using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Reports whether a unit can shoot, for the squad HUD. States that the status label
/// already names are left to it rather than repeated.
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
        // Destroyed, shut down and immobile are already spelled out by the status label beside
        // this one. Repeating them put the same word on the card twice, so the hint stays quiet
        // and only reports the one thing status does not cover: whether the unit can shoot.
        if (value is not IUnit unit) return string.Empty;
        if (unit.IsDestroyed || unit.IsShutdown || unit.IsImmobile) return string.Empty;

        return _localizationService.GetString(unit.CanFireWeapons
            ? "UnitHud_ActionWeaponsOnline"
            : "UnitHud_ActionWeaponsUnavailable");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
