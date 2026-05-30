using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Sanet.MakaMek.Avalonia.Controls.Converters;

public class BoolToBrushConverter : IValueConverter
{
    public IBrush? TrueBrush { get; set; } = new SolidColorBrush(Color.Parse("#6B8E23"));
    public IBrush? FalseBrush { get; set; } = Brushes.Transparent;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!targetType.IsAssignableTo(typeof(IBrush)))
            return Brushes.Transparent;

        return value is true ? (TrueBrush ?? Brushes.Transparent) : (FalseBrush ?? Brushes.Transparent);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
