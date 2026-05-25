using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Avalonia.Converters;

public class SegmentEventToTextConverter : IValueConverter
{
    private static ILocalizationService? _localizationService;

    public static void Initialize(ILocalizationService localization)
    {
        _localizationService = localization;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not (SegmentEvent segmentEvent, HexCoordinates location) || _localizationService == null)
            return string.Empty;

        var key = $"SegmentEvent_{segmentEvent.Type}";
        var eventLabel = _localizationService.GetString(key);

        return $"{eventLabel} @{location} ";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
