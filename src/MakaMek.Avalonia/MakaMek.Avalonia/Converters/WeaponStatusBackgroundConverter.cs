using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts a weapon's status to an appropriate background color
/// </summary>
public class WeaponStatusBackgroundConverter : IValueConverter
{
    /// <summary>
    /// Converts a weapon's status to a background color
    /// </summary>
    /// <param name="value">Weapon object</param>
    /// <param name="targetType">Target type (should be IBrush)</param>
    /// <param name="parameter">Optional parameter (not used)</param>
    /// <param name="culture">Culture info</param>
    /// <returns>Background color brush based on weapon status</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Weapon weapon || !targetType.IsAssignableTo(typeof(IBrush)))
            return new SolidColorBrush(Colors.Transparent);

        // If weapon is destroyed, return red
        if (weapon.IsDestroyed)
            return new SolidColorBrush(Colors.Red);
                
        // If weapon is not available but not destroyed, return gray (disabled)
        if (!weapon.IsAvailable)
            return new SolidColorBrush(Colors.Gray);
                
        // If weapon is available, return transparent (no effect)
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}