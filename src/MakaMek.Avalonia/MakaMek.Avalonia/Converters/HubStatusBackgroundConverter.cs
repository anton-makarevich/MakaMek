using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Sanet.MakaMek.Avalonia.Controls.Services;
using Sanet.Transport.SignalR.Client.Relay;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts a relay hub status to an appropriate background color
/// </summary>
public class HubStatusBackgroundConverter : IValueConverter
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

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!targetType.IsAssignableTo(typeof(IBrush)))
            return new SolidColorBrush(Colors.Transparent);

        var status = value switch
        {
            HubStatus s => s,
            _ => HubStatus.Unknown
        };

        return status switch
        {
            HubStatus.Online => _resourcesLocator?.TryFindResource("SuccessBrush") as IBrush ?? new SolidColorBrush(Colors.Green),
            HubStatus.Offline => _resourcesLocator?.TryFindResource("ErrorBrush") as IBrush ?? new SolidColorBrush(Colors.Red),
            HubStatus.Checking => _resourcesLocator?.TryFindResource("InfoBrush") as IBrush ?? new SolidColorBrush(Colors.DodgerBlue),
            _ => _resourcesLocator?.TryFindResource("OverlayTransparentBrush") as IBrush ?? new SolidColorBrush(Colors.Gray)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
