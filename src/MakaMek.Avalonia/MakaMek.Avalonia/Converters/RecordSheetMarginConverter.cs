using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Positions the inspected unit drawer so it clears the squad bar.
///
/// On a compact layout the drawer sits against the bottom edge, which the squad bar also owns. A
/// constant inset was wrong the moment the bar's height changed: 88 was reserved against a bar that
/// needs 118, so the drawer was drawn across the top of the cards. The inset is taken from the
/// bar's rendered height instead, and a hidden bar gives the drawer the whole edge.
///
/// On a desktop layout the drawer is pinned top right and the bar is irrelevant.
/// </summary>
public sealed class RecordSheetMarginConverter : IMultiValueConverter
{
    /// <summary>Gap between the drawer and the squad bar.</summary>
    private const double Gutter = 10;

    /// <summary>Inset used on a compact layout when there is no squad bar to clear.</summary>
    private const double CompactBottom = 88;

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 3) return AvaloniaProperty.UnsetValue;

        var compact = values[0] is true;
        if (!compact) return new Thickness(0, 60, 88, 80);

        var barShowing = values[1] is true;
        var barHeight = values[2] is double height && double.IsFinite(height) && height > 0 ? height : 0;

        return new Thickness(8, 56, 8, barShowing && barHeight > 0 ? barHeight + Gutter : CompactBottom);
    }

    public object ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}
