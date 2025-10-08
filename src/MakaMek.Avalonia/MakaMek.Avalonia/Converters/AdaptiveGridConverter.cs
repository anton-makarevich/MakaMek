using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Sanet.MakaMek.Avalonia.Converters;

public class AdaptiveGridConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Size size)
            return parameter is GridOrientation.Rows ? 2 : 1;
            
        var isHorizontal = size.Width > size.Height;
        
        if (parameter is GridOrientation orientationParam)
        {
            return orientationParam == GridOrientation.Rows 
                ? isHorizontal ? 1 : 2 
                : isHorizontal ? 2 : 1;
        }
        
        // Default to columns behavior
        return isHorizontal ? 2 : 1;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
