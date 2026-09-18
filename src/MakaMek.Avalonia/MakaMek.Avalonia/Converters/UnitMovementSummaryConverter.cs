using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Formats the movement points remaining under each movement mode for the squad HUD.
/// </summary>
public sealed class UnitMovementSummaryConverter : IValueConverter
{
    private readonly ILocalizationService _localizationService;

    /// <summary>
    /// Creates a converter that resolves movement summaries through localization.
    /// </summary>
    /// <param name="localizationService">Service used to resolve the movement format.</param>
    public UnitMovementSummaryConverter(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IUnit unit) return _localizationService.GetString("UnitHud_MovementUnavailable");
        var walk = unit.GetMovementPoints(MovementType.Walk);
        var run = unit.GetMovementPoints(MovementType.Run);
        return string.Format(culture, _localizationService.GetString("UnitHud_MovementSummary"), walk, run);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
