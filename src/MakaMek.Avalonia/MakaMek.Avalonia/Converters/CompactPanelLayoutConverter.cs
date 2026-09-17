using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Selects drawer layout values for mobile versus desktop battle-map presentation.
/// </summary>
public sealed class CompactPanelLayoutConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var compact = value is true;
        return parameter?.ToString() switch
        {
            "horizontal" => compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Right,
            "vertical" => compact ? VerticalAlignment.Bottom : VerticalAlignment.Top,
            "margin" => compact ? new Thickness(8, 56, 8, 88) : new Thickness(0, 60, 88, 80),
            "controlsHorizontal" => HorizontalAlignment.Right,
            "controlsVertical" => compact ? VerticalAlignment.Top : VerticalAlignment.Bottom,
            "controlsMargin" => compact ? new Thickness(8, 60, 8, 0) : new Thickness(0, 0, 12, 20),
            "squadMargin" => compact ? new Thickness(12, 0, 12, 10) : new Thickness(12, 0, 0, 10),
            "actionMargin" => compact ? new Thickness(0, 0, 0, 96) : new Thickness(0, 0, 0, 20),
            "maxWidth" => compact ? 1000d : 420d,
            "maxHeight" => compact ? 500d : 620d,
            _ => AvaloniaProperty.UnsetValue
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
