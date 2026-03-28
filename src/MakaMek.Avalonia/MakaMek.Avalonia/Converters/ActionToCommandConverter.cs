using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Sanet.MakaMek.Avalonia.Models;

namespace Sanet.MakaMek.Avalonia.Converters;

public class ActionToCommandConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Action action)
            return new LambdaCommand(action);
        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
