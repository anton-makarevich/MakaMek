using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Sanet.Transport.SignalR.Client.Relay;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts a relay hub status to a localized readable status string
/// </summary>
public class HubStatusTextConverter : IValueConverter
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
            HubStatus s => s,
            _ => HubStatus.Unknown
        };

        return status switch
        {
            HubStatus.Online => _localizationService?.GetString("Hub_Status_Online") ?? "Online",
            HubStatus.Offline => _localizationService?.GetString("Hub_Status_Offline") ?? "Offline",
            HubStatus.Checking => _localizationService?.GetString("Hub_Status_Checking") ?? "Checking...",
            _ => _localizationService?.GetString("Hub_Status_Unknown") ?? "Unknown"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
