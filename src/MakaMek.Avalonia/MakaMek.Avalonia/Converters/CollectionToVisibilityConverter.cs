using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converter that returns true if a collection has any items, false otherwise.
/// Used for controlling visibility of UI elements that display collections.
/// </summary>
public class CollectionToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not System.Collections.IEnumerable enumerable) return false;
        foreach (var _ in enumerable)
        {
            return true;
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
