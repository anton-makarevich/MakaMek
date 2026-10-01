using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Sanet.MakaMek.Avalonia.Controls.Services;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts a unit's current status into a status color for compact HUD cards.
/// </summary>
public class UnitStatusToBrushConverter : IValueConverter
{
    private readonly IAvaloniaResourcesLocator _resourcesLocator;

    public UnitStatusToBrushConverter(IAvaloniaResourcesLocator resourcesLocator)
    {
        _resourcesLocator = resourcesLocator;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value is UnitStatus unitStatus ? unitStatus : UnitStatus.None;

        var key = status switch
        {
            _ when status.HasFlag(UnitStatus.Destroyed) => "ErrorBrush",
            _ when status.HasFlag(UnitStatus.Shutdown) => "WarningBrush",
            _ when status.HasFlag(UnitStatus.Immobile) || status.HasFlag(UnitStatus.Prone) => "DamagedBrush",
            _ when status.HasFlag(UnitStatus.Active) => "SuccessBrush",
            _ => "BorderBrush"
        };

        return _resourcesLocator.TryFindResource(key) ?? new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
