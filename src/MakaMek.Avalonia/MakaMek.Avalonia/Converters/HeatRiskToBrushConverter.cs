using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts current heat into a progressively urgent color for the squad HUD.
/// The thresholds mirror the standard heat bands used by the game rules.
/// </summary>
public sealed class HeatRiskToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var heat = value is int currentHeat ? currentHeat : 0;
        var color = heat >= 20 ? "#B3261E" : heat >= 10 ? "#C77700" : heat >= 5 ? "#A65E00" : "#2E7D32";
        return new SolidColorBrush(Color.Parse(color));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
