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
    private readonly IAvaloniaResourcesLocator _resourcesLocator;

    /// <summary>
    /// Creates a new instance of ComponentStatusBackgroundConverter with a specific resources locator
    /// </summary>
    /// <param name="resourcesLocator">The resource locator to use</param>
    public ComponentStatusBackgroundConverter(IAvaloniaResourcesLocator resourcesLocator)
    {
        _resourcesLocator = resourcesLocator;
    }

    /// <summary>
    /// Creates a new instance of ComponentStatusBackgroundConverter with the default resources locator
    /// </summary>
    public ComponentStatusBackgroundConverter() : this(new AvaloniaResourcesLocator())
    {
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
            ComponentStatus.Destroyed => _resourcesLocator.TryFindResource("DestroyedBrush") ?? new SolidColorBrush(Colors.Red),
            ComponentStatus.Damaged => _resourcesLocator.TryFindResource("DamagedBrush") ?? new SolidColorBrush(Colors.Orange),
            ComponentStatus.Active => new SolidColorBrush(Colors.Transparent),
            _ => new SolidColorBrush(Colors.Gray)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}