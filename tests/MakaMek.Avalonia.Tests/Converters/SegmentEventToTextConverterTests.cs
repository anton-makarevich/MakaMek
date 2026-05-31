using NSubstitute;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;
using Shouldly;
using System.Globalization;
using Sanet.MakaMek.Map.Models.MovementCosts;

namespace MakaMek.Avalonia.Tests.Converters;

public class SegmentEventToTextConverterTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly SegmentEventToTextConverter _sut = new();

    public SegmentEventToTextConverterTests()
    {
        _localizationService.GetString("SegmentEvent_Fall").Returns("Fall");
        _localizationService.GetString("SegmentEvent_StandupAttempt").Returns("Standup");

        SegmentEventToTextConverter.Initialize(_localizationService);
    }

    [Fact]
    public void Convert_WithNullValue_ReturnsEmptyString()
    {
        var result = _sut.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        result.ShouldBeOfType<string>();
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Convert_WithFallEvent_ReturnsLocalizedText()
    {
        var segmentEvent = new SegmentEvent(SegmentEventType.Fall, []);
        var location = new HexCoordinates(1, 2);
        var value = (segmentEvent, location);
        const string expectedText = "Fall @0102";

        var result = _sut.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);

        result.ShouldBeOfType<string>();
        result.ShouldBe(expectedText);
    }

    [Fact]
    public void Convert_WithStandupAttemptEvent_ReturnsLocalizedText()
    {
        var segmentEvent = new SegmentEvent(SegmentEventType.StandupAttempt, [new StandUpAttemptMovementCost { Value = 1 }]);
        var location = new HexCoordinates(3, 4);
        var value = (segmentEvent, location);
        const string expectedText = "Standup @0304";

        var result = _sut.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);

        result.ShouldBeOfType<string>();
        result.ShouldBe(expectedText);
    }

    [Fact]
    public void Convert_WithNonTupleValue_ReturnsEmptyString()
    {
        var notATuple = new object();

        var result = _sut.Convert(notATuple, typeof(string), null, CultureInfo.InvariantCulture);

        result.ShouldBeOfType<string>();
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        Should.Throw<NotSupportedException>(() =>
            _sut.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture));
    }
}
