using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Sanet.MakaMek.Avalonia.Converters;

public class SlotsRangeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int[] slots || slots.Length == 0)
            return "-";
        if (slots.Length == 1)
            return slots[0].ToString();
        Array.Sort(slots);
        // If slots are consecutive, show as range
        var consecutive = true;
        for (var i = 1; i < slots.Length; i++)
        {
            if (slots[i] == slots[i - 1] + 1) continue;
            consecutive = false;
            break;
        }
        return consecutive ? $"{slots[0]}-{slots[^1]}" :
            // Otherwise, comma-separated
            string.Join(",", slots);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
