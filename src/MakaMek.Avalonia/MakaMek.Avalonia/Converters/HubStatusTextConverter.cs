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
    private readonly ILocalizationService _localizationService;

    public HubStatusTextConverter(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
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
            HubStatus.Online => _localizationService.GetString("Hub_Status_Online"),
            HubStatus.Offline => _localizationService.GetString("Hub_Status_Offline"),
            HubStatus.Checking => _localizationService.GetString("Hub_Status_Checking"),
            _ => _localizationService.GetString("Hub_Status_Unknown")
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
