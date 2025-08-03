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
    private readonly IAvaloniaResourcesLocator _resourcesLocator;

    /// <summary>
    /// Creates a new instance of ConsciousnessColorConverter with a specific resources locator
    /// </summary>
    /// <param name="resourcesLocator">The resource locator to use</param>
    public ConsciousnessColorConverter(IAvaloniaResourcesLocator resourcesLocator)
    {
        _resourcesLocator = resourcesLocator;
    }

    /// <summary>
    /// Creates a new instance of ConsciousnessColorConverter with the default resources locator
    /// </summary>
    public ConsciousnessColorConverter() : this(new AvaloniaResourcesLocator())
    {
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool isConscious)
            return _resourcesLocator.TryFindResource("WarningColor") ?? Colors.Gray;

        return isConscious 
            ? _resourcesLocator.TryFindResource("SuccessColor") ?? Colors.Green
            : _resourcesLocator.TryFindResource("ErrorColor") ?? Colors.Red;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
