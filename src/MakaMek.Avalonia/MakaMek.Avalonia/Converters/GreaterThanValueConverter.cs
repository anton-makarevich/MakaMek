using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts a numeric value to a boolean indicating if it's greater than the specified value
/// If no parameter is provided, defaults to checking if value is greater than 1
/// </summary>
public class GreaterThanValueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int intValue) return false;
        var compareValue = 1; // Default to 1
            
        // If parameter is provided, use it as the comparison value
        if (parameter is string strParam && int.TryParse(strParam, out var paramValue))
        {
            compareValue = paramValue;
        }
            
        return intValue > compareValue;

    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
