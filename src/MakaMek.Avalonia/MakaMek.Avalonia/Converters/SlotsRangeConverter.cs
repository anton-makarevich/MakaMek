using System;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace Sanet.MakaMek.Avalonia.Converters;

public class SlotsRangeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int[] slots || slots.Length == 0)
            return "-";
        var indices = slots.Select(i => i + 1).OrderBy(i => i).ToArray();
        var result = new System.Collections.Generic.List<string>();
        int start = indices[0], end = indices[0];
        for (var i = 1; i < indices.Length; i++)
        {
            if (indices[i] == end + 1)
            {
                end = indices[i];
            }
            else
            {
                result.Add(start == end ? $"{start}" : $"{start}-{end}");
                start = end = indices[i];
            }
        }
        result.Add(start == end ? $"{start}" : $"{start}-{end}");
        return string.Join(",", result);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
