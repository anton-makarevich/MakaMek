using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Keeps the squad bar clear of the map controls drawer without leaving the rest of the bottom
/// edge empty.
///
/// The bar stretches the full width and insets on the right by however much the drawer occupies,
/// but only while the drawer is actually showing. A hidden control keeps the bounds it was last
/// arranged with, so width alone would hold the gap open after the drawer closed.
///
/// The inset reads the drawer's rendered width rather than a constant, so a longer translation of
/// a button label cannot leave the two overlapping.
/// </summary>
public sealed class SquadBarMarginConverter : IMultiValueConverter
{
    /// <summary>Gap between the last card and the drawer.</summary>
    private const double Gutter = 12;

    private const double Edge = 12;
    private const double Bottom = 10;

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 3) return AvaloniaProperty.UnsetValue;

        var compact = values[0] is true;
        var drawerShowing = values[1] is true;
        var drawerWidth = values[2] is double width && double.IsFinite(width) && width > 0 ? width : 0;

        // On a compact layout the drawer sits at the top of the screen, so the bottom edge is the
        // bar's alone.
        var reserve = !compact && drawerShowing && drawerWidth > 0;

        return new Thickness(Edge, 0, reserve ? drawerWidth + Gutter : Edge, Bottom);
    }

    public object ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}
