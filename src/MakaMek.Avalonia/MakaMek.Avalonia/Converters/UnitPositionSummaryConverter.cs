using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Formats a deployed unit's grid coordinates and facing for compact tactical summaries.
/// </summary>
public sealed class UnitPositionSummaryConverter : IValueConverter
{
    private readonly ILocalizationService _localizationService;

    /// <summary>
    /// Creates a converter that resolves position fallback text through localization.
    /// </summary>
    /// <param name="localizationService">Service used to resolve HUD labels.</param>
    public UnitPositionSummaryConverter(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not HexPosition position) return _localizationService.GetString("UnitHud_PositionUnavailable");
        return $"Q{position.Coordinates.Q}/R{position.Coordinates.R} • {position.Facing}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
