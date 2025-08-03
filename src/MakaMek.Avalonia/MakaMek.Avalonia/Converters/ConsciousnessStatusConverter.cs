using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts a boolean consciousness value to a readable status string
/// </summary>
public class ConsciousnessStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool isConscious)
            return "UNKNOWN";

        return isConscious ? "CONSCIOUS" : "UNCONSCIOUS";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
