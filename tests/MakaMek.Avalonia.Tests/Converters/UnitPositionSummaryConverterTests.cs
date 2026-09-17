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
        return service;
    }

    [Fact]
    public void Convert_FormatsCoordinatesAndFacing()
    {
        var position = new HexPosition(3, 5, HexDirection.BottomRight);

        var result = new UnitPositionSummaryConverter(Localization()).Convert(position, typeof(string), null, CultureInfo.InvariantCulture);

        result.ShouldBe("Q3/R5 • BottomRight");
    }

    [Fact]
    public void Convert_Null_ReturnsOffMap()
    {
        var result = new UnitPositionSummaryConverter(Localization()).Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        result.ShouldBe("Off map");
    }
}
