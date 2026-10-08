using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Provides the compact pin toggle label used by the inspected-unit drawer.
/// </summary>
public class DrawerPinTextConverter : IValueConverter
{
    private readonly ILocalizationService _localizationService;

    public DrawerPinTextConverter(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => _localizationService.GetString(value is true ? "UnitHud_UnpinDrawer" : "UnitHud_PinDrawer");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
