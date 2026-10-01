using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Sanet.MakaMek.Avalonia.Controls.Services;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts current heat into a progressively urgent color for the squad HUD.
/// </summary>
public class HeatRiskToBrushConverter : IValueConverter
{
    private readonly IAvaloniaResourcesLocator _resourcesLocator;

    public HeatRiskToBrushConverter(IAvaloniaResourcesLocator resourcesLocator)
    {
        _resourcesLocator = resourcesLocator;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var heat = value is int currentHeat ? currentHeat : 0;

        var key = heat switch
        {
            >= 20 => "ErrorBrush",
            >= 10 => "WarningBrush",
            >= 5 => "DamagedBrush",
            _ => "SuccessBrush"
        };

        return _resourcesLocator.TryFindResource(key) ?? new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
