using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace Sanet.MakaMek.Avalonia.Converters;

public class AdaptiveColumnsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Size size)
            return 1; // Default orientation if the value is not a Size object
        var isHorizontal = size.Width > size.Height;
        return isHorizontal ? 2 : 1;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}