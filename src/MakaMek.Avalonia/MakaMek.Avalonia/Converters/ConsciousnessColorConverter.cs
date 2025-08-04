using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Sanet.MakaMek.Avalonia.Services;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts a pilot consciousness status (boolean) to an appropriate color
/// </summary>
public class ConsciousnessColorConverter : IValueConverter
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
        if (value is not bool isConscious)
            return _resourcesLocator?.TryFindResource("WarningColor") ?? Colors.Gray;

        return isConscious 
            ? _resourcesLocator?.TryFindResource("SuccessColor") ?? Colors.Green
            : _resourcesLocator?.TryFindResource("ErrorColor") ?? Colors.Red;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
