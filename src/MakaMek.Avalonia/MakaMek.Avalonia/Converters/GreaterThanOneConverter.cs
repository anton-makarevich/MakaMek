using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts a numeric value to a boolean indicating if it's greater than one
/// </summary>
public class GreaterThanOneConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
            return intValue > 1;
            
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
