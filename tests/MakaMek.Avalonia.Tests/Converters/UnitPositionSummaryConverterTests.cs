using System.Globalization;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Localization;
using NSubstitute;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class UnitPositionSummaryConverterTests
{
    private static ILocalizationService Localization()
    {
        var service = Substitute.For<ILocalizationService>();
        service.GetString("UnitHud_PositionUnavailable").Returns("Off map");
        service.GetString("UnitHud_PositionSummary").Returns("Q{0}/R{1} · {2}");
        service.GetString("HexDirection_BottomRight").Returns("SE");
        service.GetString("HexDirection_Top").Returns("N");
        return service;
    }

    [Fact]
    public void Convert_FormatsCoordinatesAndFacing()
    {
        var position = new HexPosition(3, 5, HexDirection.BottomRight);

        var result = new UnitPositionSummaryConverter(Localization()).Convert(position, typeof(string), null, CultureInfo.InvariantCulture);

        result.ShouldBe("Q3/R5 · SE");
    }

    /// <summary>
    /// The facing is an enum. Printed directly it put "BottomRight" on a card whose other labels
    /// are all translated.
    /// </summary>
    [Fact]
    public void Convert_TranslatesTheFacing_RatherThanPrintingTheEnum()
    {
        var result = new UnitPositionSummaryConverter(Localization())
            .Convert(new HexPosition(1, 1, HexDirection.Top), typeof(string), null, CultureInfo.InvariantCulture);

        result.ShouldBe("Q1/R1 · N");
        result.ToString().ShouldNotContain("Top");
    }

    [Fact]
    public void Convert_Null_ReturnsOffMap()
    {
        var result = new UnitPositionSummaryConverter(Localization()).Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        result.ShouldBe("Off map");
    }

    [Fact]
    public void ConvertBack_IsNotSupported()
        => Should.Throw<NotSupportedException>(() => new UnitPositionSummaryConverter(Localization())
            .ConvertBack("Off map", typeof(object), null, CultureInfo.InvariantCulture));
}
