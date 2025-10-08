using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace Sanet.MakaMek.Avalonia.Converters;

public class AdaptiveOrientationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Size size)
            return Orientation.Vertical; // Default orientation if the value is not a Size object
        var isHorizontal = size.Width > size.Height;
        return isHorizontal ? Orientation.Horizontal : Orientation.Vertical;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}