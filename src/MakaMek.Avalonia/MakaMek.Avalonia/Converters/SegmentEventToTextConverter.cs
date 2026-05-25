using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Avalonia.Converters;

public class SegmentEventToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not (SegmentEvent segmentEvent, HexCoordinates location))
            return string.Empty;

        var eventLabel = segmentEvent.Type switch
        {
            SegmentEventType.Fall => "Fall",
            SegmentEventType.StandupAttempt => "Standup",
            _ => segmentEvent.Type.ToString()
        };

        return $"{eventLabel} @ ({location.H},{location.V})";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
