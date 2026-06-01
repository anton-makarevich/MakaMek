using NSubstitute;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;
using System.Globalization;

namespace MakaMek.Avalonia.Tests.Converters;

public class MovementBreakdownConverterTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly MovementBreakdownConverter _sut = new();

    public MovementBreakdownConverterTests()
    {
        _localizationService.GetString("MovementCost_Terrain").Returns("entered {0}, {1} MP");
        _localizationService.GetString("Terrain_Clear").Returns("Clear");
        _localizationService.GetString("Terrain_LightWoods").Returns("Light Woods");
        _localizationService.GetString("Terrain_HeavyWoods").Returns("Heavy Woods");
        _localizationService.GetString("Terrain_Rough").Returns("Rough");
        _localizationService.GetString("Terrain_Water").Returns("Water");
        MovementBreakdownConverter.Initialize(_localizationService);
    }

    [Fact]
    public void Convert_WithNullValue_ReturnsEmptyString()
    {
        var result = _sut.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        result.ShouldBeOfType<string>();
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Convert_WithMovementPath_ReturnsRenderedString()
    {
        var segments = new List<PathSegment>
        {
            new(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }])
        };
        var path = new MovementPath(segments, MovementType.Walk);

        var result = _sut.Convert(path, typeof(string), null, CultureInfo.InvariantCulture);

        result.ShouldBeOfType<string>();
        result.ShouldBe($"1. 0101:0->0102:0{Environment.NewLine}- entered Clear, 2 MP");
    }

    [Fact]
    public void Convert_WithNonPathValue_ReturnsEmptyString()
    {
        var result = _sut.Convert(new object(), typeof(string), null, CultureInfo.InvariantCulture);

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
