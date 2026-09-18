using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts the flags on a unit into the single most important operational state for the HUD.
/// </summary>
public sealed class UnitStatusTextConverter : IValueConverter
{
    private readonly ILocalizationService _localizationService;

    /// <summary>
    /// Creates a converter that resolves unit status text through localization.
    /// </summary>
    /// <param name="localizationService">Service used to resolve status labels.</param>
    public UnitStatusTextConverter(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value is UnitStatus unitStatus ? unitStatus : UnitStatus.None;

        return status.HasFlag(UnitStatus.Destroyed) ? Format("✖", "UnitHud_StatusDestroyed") :
            status.HasFlag(UnitStatus.Shutdown) ? Format("⚠", "UnitHud_StatusShutdown") :
            status.HasFlag(UnitStatus.Immobile) ? Format("◆", "UnitHud_StatusImmobile") :
            status.HasFlag(UnitStatus.Prone) ? Format("↘", "UnitHud_StatusProne") :
            status.HasFlag(UnitStatus.Active) ? Format("●", "UnitHud_StatusOperational") :
            Format("?", "UnitHud_StatusUnavailable");
    }

    private string Format(string symbol, string key) => $"{symbol} {_localizationService.GetString(key)}";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
