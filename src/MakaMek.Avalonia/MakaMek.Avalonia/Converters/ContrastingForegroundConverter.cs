using System;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Sanet.MakaMek.Avalonia.Converters;

public class ContrastingForegroundConverter : IValueConverter
{
    public object Convert(
        object? value, Type targetType, 
        object? parameter,
        System.Globalization.CultureInfo? culture)
    {
        if (value is not string hexColor) return new SolidColorBrush(Colors.Black);
        try
        {
            var color = Color.Parse(hexColor);
            // If color is close to white, return black, otherwise return white
            // Using standard luminance calculation
            var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
            return new SolidColorBrush(luminance > 0.5 ? Colors.Black : Colors.White);
        }
        catch
        {
            return new SolidColorBrush(Colors.Black);
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter,
        System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
