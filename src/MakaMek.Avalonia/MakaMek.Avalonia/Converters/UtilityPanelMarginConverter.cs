using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Lifts the bottom-right utility panels clear of the squad bar.
///
/// GamePanel draws its card against the bottom-right corner and knows nothing about what else is
/// down there, so the command log and the map settings were drawn over the squad bar. The inset is
/// taken from the bar's rendered height rather than a constant, so it cannot fall behind a change
/// to the card size.
/// </summary>
public sealed class UtilityPanelMarginConverter : IMultiValueConverter
{
    /// <summary>Gap between the panel and the squad bar.</summary>
    private const double Gutter = 10;

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return AvaloniaProperty.UnsetValue;

        var barShowing = values[0] is true;
        var barHeight = values[1] is double height && double.IsFinite(height) && height > 0 ? height : 0;

        return new Thickness(0, 0, 0, barShowing && barHeight > 0 ? barHeight + Gutter : 0);
    }

    public object ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}
