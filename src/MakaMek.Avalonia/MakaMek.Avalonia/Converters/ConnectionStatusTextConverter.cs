using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts a transport connection status to a localized readable status string
/// </summary>
public class ConnectionStatusTextConverter : IValueConverter
{
    private static ILocalizationService? _localizationService;

    public static void Initialize(ILocalizationService localization)
    {
        _localizationService = localization;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value switch
        {
            ConnectionStatus s => s,
            _ => ConnectionStatus.Disconnected
        };

        return status switch
        {
            ConnectionStatus.NotConnected => _localizationService?.GetString("Connection_Status_NotConnected") ?? "Not connected",
            ConnectionStatus.Connecting => _localizationService?.GetString("Connection_Status_Connecting") ?? "Connecting...",
            ConnectionStatus.Connected => _localizationService?.GetString("Connection_Status_Connected") ?? "Connected",
            ConnectionStatus.Reconnecting => _localizationService?.GetString("Connection_Status_Reconnecting") ?? "Reconnecting...",
            ConnectionStatus.Disconnected => _localizationService?.GetString("Connection_Status_Disconnected") ?? "Disconnected",
            _ => _localizationService?.GetString("Connection_Status_Closed") ?? "Connection closed"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}