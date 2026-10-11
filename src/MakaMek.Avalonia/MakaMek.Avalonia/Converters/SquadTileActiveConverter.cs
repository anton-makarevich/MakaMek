using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Tells a squad tile whether its unit is the one being acted with. The first value is the tile's
/// unit; it is active when it is any of the values that follow (the selected unit, the attacker).
/// </summary>
public sealed class SquadTileActiveConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] is not { } unit) return false;
        for (var i = 1; i < values.Count; i++)
        {
            if (ReferenceEquals(values[i], unit)) return true;
        }

        return false;
    }

    public object ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}
