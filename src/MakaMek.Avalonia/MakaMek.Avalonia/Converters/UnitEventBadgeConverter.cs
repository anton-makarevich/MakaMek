using System;
using System.Globalization;
using System.Collections;
using System.Linq;
using Avalonia.Data.Converters;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Formats a unit notification collection as a compact recent-event badge.
/// </summary>
public sealed class UnitEventBadgeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value is IEnumerable collection ? collection.Cast<object>().Count() : 0;
        return count == 0 ? string.Empty : $"⚠ {count}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
