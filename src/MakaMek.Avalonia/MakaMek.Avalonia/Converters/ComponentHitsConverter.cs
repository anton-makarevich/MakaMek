using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Sanet.MakaMek.Core.Models.Units.Components;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts a Component to a string showing its current hits and total health points
/// </summary>
public class ComponentHitsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Component component)
            return string.Empty;

        // Only show hit information for components with multiple health points
        if (component.HealthPoints <= 1)
            return string.Empty;

        return $"{component.Hits}/{component.HealthPoints}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
