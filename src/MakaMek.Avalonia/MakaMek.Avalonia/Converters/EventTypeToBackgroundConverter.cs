using Avalonia.Data.Converters;
using Avalonia.Media;
using Sanet.MakaMek.Core.Events;
using System;
using System.Globalization;
using Sanet.MakaMek.Avalonia.Services;

namespace Sanet.MakaMek.Avalonia.Converters
{
    /// <summary>
    /// Converts a UiEventType to a background brush
    /// </summary>
    public class EventTypeToBackgroundConverter : IValueConverter
    {
        private static IAvaloniaResourcesLocator? _resourcesLocator;

        /// <summary>
        /// Initializes the converter with the resources locator
        /// </summary>
        /// <param name="resourcesLocator">The resource locator to use</param>
        public static void Initialize(IAvaloniaResourcesLocator resourcesLocator)
        {
            _resourcesLocator = resourcesLocator;
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is UiEventType eventType)
            {
                return eventType switch
                {
                    UiEventType.ArmorDamage => _resourcesLocator?.TryFindResource("MechArmorBrush") as SolidColorBrush
                                               ?? new SolidColorBrush(Colors.LightBlue),
                    UiEventType.StructureDamage => _resourcesLocator?.TryFindResource("MechStructureBrush") as SolidColorBrush
                                                  ?? new SolidColorBrush(Colors.Orange),
                    _ => _resourcesLocator?.TryFindResource("DestroyedColor") as SolidColorBrush
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
