using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Sanet.MakaMek.Avalonia.Services;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts a boolean to a brush - used for selection highlighting
/// </summary>
public class ISelectedItemToBrushConverter : IValueConverter
{
    private static IAvaloniaResourcesLocator? _resourcesLocator;

    /// <summary>
    /// Initializes the converter with the resources locator
    /// </summary>
    /// <param name="resourcesLocator">The resource locator to use</param>
    public static void Initialize(IAvaloniaResourcesLocator resourcesLocator)
    {
        _resourcesLocator = resourcesLocator;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!targetType.IsAssignableTo(typeof(IBrush)))
            return Brushes.Transparent;

        return value is true 
            ? (_resourcesLocator?.TryFindResource("PrimaryBrush") ?? new SolidColorBrush(Color.Parse("#6B8E23")))
            : Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
