using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Avalonia.Converters;

public class WeaponRangeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Weapon weapon)
            return null;

        var minRange = weapon.Range.MinimumRange == 0 ? "-" : weapon.Range.MinimumRange.ToString();
        return $"{minRange}|{weapon.Range.ShortRange}|{weapon.Range.MediumRange}|{weapon.Range.LongRange}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}