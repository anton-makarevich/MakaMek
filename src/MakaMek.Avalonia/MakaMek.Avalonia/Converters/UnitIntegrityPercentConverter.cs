using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Formats a current/max armor or structure pair as a rounded percentage for compact HUD cards.
/// </summary>
public sealed class UnitIntegrityPercentConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || !TryGetInt(values[0], out var current) || !TryGetInt(values[1], out var maximum) || maximum <= 0)
            return "0%";

        return $"{Math.Clamp((int)Math.Round(current * 100d / maximum), 0, 100)}%";
    }

    public object ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;

    private static bool TryGetInt(object? value, out int result)
    {
        result = value switch
        {
            int number => number,
            _ => 0
        };
        return value is int;
    }
}
