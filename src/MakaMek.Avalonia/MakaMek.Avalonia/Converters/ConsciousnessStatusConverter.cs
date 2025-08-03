using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts a boolean consciousness value to a localized readable status string
/// </summary>
public class ConsciousnessStatusConverter : IValueConverter
{
    private static ILocalizationService? _localizationService;

    public static void Initialize(ILocalizationService localization)
    {
        _localizationService = localization;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool isConscious || _localizationService == null)
            return _localizationService?.GetString("Pilot_Status_Unknown") ?? "UNKNOWN";

        return isConscious
            ? _localizationService.GetString("Pilot_Status_Conscious")
            : _localizationService.GetString("Pilot_Status_Unconscious");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
