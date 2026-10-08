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
/// The top inset comes from the turn status bar for the same reason: it is 50 points with just
/// the turn and phase on it, and taller once the guidance labels fill in, so a constant there was
/// also wrong.
/// </summary>
public sealed class RecordSheetMarginConverter : IMultiValueConverter
{
    /// <summary>Gap between the drawer and the squad bar.</summary>
    private const double Gutter = 10;

    /// <summary>Inset used on a compact layout when there is no squad bar to clear.</summary>
    private const double CompactBottom = 88;

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 4) return AvaloniaProperty.UnsetValue;

        var compact = values[0] is true;
        var barShowing = values[1] is true;
        var barHeight = Size(values[2]);
        var top = Size(values[3]) + Gutter;

        if (!compact) return new Thickness(0, top, 88, 80);

        return new Thickness(8, top, 8, barShowing && barHeight > 0 ? barHeight + Gutter : CompactBottom);
    }

    private static double Size(object? value) =>
        value is double size && double.IsFinite(size) && size > 0 ? size : 0;

    public object ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}
