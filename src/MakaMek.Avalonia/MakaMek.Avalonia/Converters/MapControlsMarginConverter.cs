using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Places the map controls column clear of the turn status bar.
///
/// On a compact layout the column sits at the top, directly under that bar, and the bar is not a
/// fixed height: it is 50 points with just the turn and phase on it, and taller once the guidance
/// labels fill in. A constant inset put the column inside it.
/// </summary>
public sealed class MapControlsMarginConverter : IMultiValueConverter
{
    private const double Gutter = 10;
    private const double Edge = 8;

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return AvaloniaProperty.UnsetValue;

        var compact = values[0] is true;
        var turnStatusHeight = values[1] is double height && double.IsFinite(height) && height > 0 ? height : 0;

        return compact
            ? new Thickness(Edge, turnStatusHeight + Gutter, Edge, 0)
            : new Thickness(0, 0, 12, 20);
    }

    public object ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}
