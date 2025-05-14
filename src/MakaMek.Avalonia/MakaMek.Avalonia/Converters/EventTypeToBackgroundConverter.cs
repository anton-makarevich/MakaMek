using Avalonia.Data.Converters;
using Avalonia.Media;
using Sanet.MakaMek.Core.Events;
using System;
using System.Globalization;
using Sanet.MakaMek.Avalonia.Utils;

namespace Sanet.MakaMek.Avalonia.Converters
{
    /// <summary>
    /// Converts a UiEventType to a background brush
    /// </summary>
    public class EventTypeToBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is UiEventType eventType)
            {
                return eventType switch
                {
                    UiEventType.ArmorDamage => AvaloniaResourcesLocator.TryFindResource("MechArmorBrush") as SolidColorBrush
                                               ?? new SolidColorBrush(Colors.LightBlue),
                    UiEventType.StructureDamage => AvaloniaResourcesLocator.TryFindResource("MechStructureBrush") as SolidColorBrush
                                                  ?? new SolidColorBrush(Colors.Orange),
                    _ => AvaloniaResourcesLocator.TryFindResource("DestroyedColor") as SolidColorBrush
                                                ?? new SolidColorBrush(Colors.Red)
                };
            }

            return null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
