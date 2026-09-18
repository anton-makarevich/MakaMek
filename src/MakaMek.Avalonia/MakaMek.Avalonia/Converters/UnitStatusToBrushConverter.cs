using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Avalonia.Converters;

/// <summary>
/// Converts a unit's current status into a high-contrast status color for compact HUD cards.
/// </summary>
public sealed class UnitStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value is UnitStatus unitStatus ? unitStatus : UnitStatus.None;
        return status switch
        {
            _ when status.HasFlag(UnitStatus.Destroyed) => new SolidColorBrush(Color.Parse("#B3261E")),
            _ when status.HasFlag(UnitStatus.Shutdown) => new SolidColorBrush(Color.Parse("#C77700")),
            _ when status.HasFlag(UnitStatus.Immobile) || status.HasFlag(UnitStatus.Prone) => new SolidColorBrush(Color.Parse("#A65E00")),
            _ when status.HasFlag(UnitStatus.Active) => new SolidColorBrush(Color.Parse("#2E7D32")),
            _ => new SolidColorBrush(Color.Parse("#616161"))
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
