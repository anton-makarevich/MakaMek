using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts a boolean consciousness value to a localized readable status string
/// </summary>
public class ConsciousnessStatusConverter : IValueConverter
{
    private readonly ILocalizationService _localizationService;

    public ConsciousnessStatusConverter(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool isConscious)
            return _localizationService.GetString("Pilot_Status_Unknown");

        return isConscious
            ? _localizationService.GetString("Pilot_Status_Conscious")
            : _localizationService.GetString("Pilot_Status_Unconscious");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
