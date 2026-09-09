using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Sanet.MakaMek.Avalonia.Controls.Services;
using Sanet.MakaMek.Core.Services.Transport;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts a transport connection status to an appropriate background color
/// </summary>
public class ConnectionStatusBackgroundConverter : IValueConverter
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
            ConnectionStatus s => s,
            _ => ConnectionStatus.Disconnected
        };

        return status switch
        {
            ConnectionStatus.NotConnected => _resourcesLocator?.TryFindResource("InfoBrush") as IBrush ?? new SolidColorBrush(Colors.DodgerBlue),
            ConnectionStatus.Connected => _resourcesLocator?.TryFindResource("SuccessBrush") as IBrush ?? new SolidColorBrush(Colors.Green),
            ConnectionStatus.Connecting => _resourcesLocator?.TryFindResource("InfoBrush") as IBrush ?? new SolidColorBrush(Colors.DodgerBlue),
            ConnectionStatus.Reconnecting => _resourcesLocator?.TryFindResource("InfoBrush") as IBrush ?? new SolidColorBrush(Colors.DodgerBlue),
            _ => _resourcesLocator?.TryFindResource("ErrorBrush") as IBrush ?? new SolidColorBrush(Colors.Red)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}