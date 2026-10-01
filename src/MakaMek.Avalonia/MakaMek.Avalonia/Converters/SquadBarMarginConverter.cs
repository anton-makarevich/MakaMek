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
/// The bar stretches the full width and insets on the right by however much the drawer actually
/// occupies. A collapsed drawer measures zero, so the bar takes the whole width on its own. The
/// inset reads the drawer's rendered width rather than a constant, so a longer translation of a
/// button label cannot leave the two overlapping.
/// </summary>
public sealed class SquadBarMarginConverter : IMultiValueConverter
{
    /// <summary>Gap between the last card and the drawer.</summary>
    private const double Gutter = 12;

    private const double Edge = 12;
    private const double Bottom = 10;

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return AvaloniaProperty.UnsetValue;

        var compact = values[0] is true;
        var drawerWidth = values[1] is double width && double.IsFinite(width) && width > 0 ? width : 0;

        // On a compact layout the drawer sits at the top of the screen, so the bottom edge is the
        // bar's alone.
        var right = compact ? Edge : drawerWidth > 0 ? drawerWidth + Gutter : Edge;

        return new Thickness(Edge, 0, right, Bottom);
    }

    public object ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}
