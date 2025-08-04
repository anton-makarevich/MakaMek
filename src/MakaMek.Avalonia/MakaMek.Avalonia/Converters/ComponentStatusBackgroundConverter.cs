using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Sanet.MakaMek.Avalonia.Services;
using Sanet.MakaMek.Core.Models.Units.Components;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts a component's status to an appropriate background color
/// </summary>
public class ComponentStatusBackgroundConverter : IValueConverter
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
            return new SolidColorBrush(Colors.Transparent);

        var status = value switch
        {
            Component c => c.Status,
            ComponentStatus s => s,
            _ => ComponentStatus.Active
        };

        return status switch
        {
            ComponentStatus.Destroyed => _resourcesLocator?.TryFindResource("DestroyedBrush") ?? new SolidColorBrush(Colors.Red),
            ComponentStatus.Damaged => _resourcesLocator?.TryFindResource("DamagedBrush") ?? new SolidColorBrush(Colors.Orange),
            ComponentStatus.Active => new SolidColorBrush(Colors.Transparent),
            _ => new SolidColorBrush(Colors.Gray)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}