using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Avalonia.Converters;

public class MovementBreakdownConverter : IValueConverter
{
    private readonly ILocalizationService _localizationService;

    public MovementBreakdownConverter(ILocalizationService localizationService)
        => _localizationService = localizationService;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not MovementPath path)
            return string.Empty;
        return path.Render(_localizationService);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
