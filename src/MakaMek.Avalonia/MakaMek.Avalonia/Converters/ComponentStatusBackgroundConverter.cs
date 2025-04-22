using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Sanet.MakaMek.Core.Models.Units.Components;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts a component's status to an appropriate background color
/// </summary>
public class ComponentStatusBackgroundConverter : IValueConverter
{
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
            ComponentStatus.Destroyed => new SolidColorBrush(Colors.Red),
            ComponentStatus.Active => new SolidColorBrush(Colors.Transparent),
            _ => new SolidColorBrush(Colors.Gray)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}