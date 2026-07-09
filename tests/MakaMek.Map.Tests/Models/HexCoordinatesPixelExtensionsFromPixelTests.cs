using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

public class HexCoordinatesPixelExtensionsFromPixelTests
{
    [Fact]
    public void FromPixel_CenterOfOrigin_ReturnsOrigin()
    {
        var result = HexCoordinatesPixelExtensions.FromPixel(
            HexCoordinatesPixelExtensions.HexWidth / 2,
            HexCoordinatesPixelExtensions.HexHeight / 2);
        result.ShouldBe(new HexCoordinates(0, 0));
    }

    [Theory]
    [InlineData(50, 43.30127018922193, 0, 0)]
    [InlineData(125, 0, 1, 0)]
    [InlineData(200, 43.30127018922193, 2, 0)]
    [InlineData(50, 129.9038105676658, 0, 1)]
    [InlineData(125, 86.60254037844386, 1, 1)]
    [InlineData(200, 129.9038105676658, 2, 1)]
    [InlineData(-25, 0, -1, 0)]
    [InlineData(50, -43.30127018922193, 0, -1)]
    public void FromPixel_HexCenter_ReturnsExactHex(
        double x, double y, int expectedQ, int expectedR)
    {
        var result = HexCoordinatesPixelExtensions.FromPixel(x, y);
        result.Q.ShouldBe(expectedQ);
        result.R.ShouldBe(expectedR);
    }

    [Fact]
    public void FromPixel_RoundTripWithHAndV_ReturnsOriginal()
    {
        var original = new HexCoordinates(3, 5);
        var h = original.H + HexCoordinatesPixelExtensions.HexWidth / 2.0;
        var v = original.V + HexCoordinatesPixelExtensions.HexHeight / 2.0;
        var result = HexCoordinatesPixelExtensions.FromPixel(h, v);
        result.ShouldBe(original);
    }

    [Fact]
    public void FromPixel_RoundTripMultipleHexes_ReturnsOriginal()
    {
        var testCases = new[]
        {
            new HexCoordinates(0, 0),
            new HexCoordinates(0, 1),
            new HexCoordinates(1, 0),
            new HexCoordinates(1, 1),
            new HexCoordinates(-1, 0),
            new HexCoordinates(0, -1),
            new HexCoordinates(5, 3),
            new HexCoordinates(-3, -2),
            new HexCoordinates(10, -5)
        };

        foreach (var original in testCases)
        {
            var h = original.H + HexCoordinatesPixelExtensions.HexWidth / 2.0;
            var v = original.V + HexCoordinatesPixelExtensions.HexHeight / 2.0;
            var result = HexCoordinatesPixelExtensions.FromPixel(h, v);
            result.ShouldBe(original, $"Failed for ({original.Q}, {original.R}) at pixel ({h}, {v})");
        }
    }

    [Fact]
    public void FromPixel_NearEdge_ReturnsCorrectHex()
    {
        // Point at (100, 10) is inside hex (1, 0) but not at its center
        var result = HexCoordinatesPixelExtensions.FromPixel(100, 10);
        result.ShouldBe(new HexCoordinates(1, 0));
    }

    [Fact]
    public void FromPixel_PointNearBottomRightOfOrigin_ReturnsCorrectHex()
    {
        // Point at (30, 30) — well inside hex (0,0)
        var result = HexCoordinatesPixelExtensions.FromPixel(30, 30);
        result.ShouldBe(new HexCoordinates(0, 0));
    }

    [Fact]
    public void FromPixel_PointNearBottomOfOrigin_ReturnsBottomHex()
    {
        // Point at (0, 70) — left of hex (0,0) center, actually inside hex (-1, 1)
        var result = HexCoordinatesPixelExtensions.FromPixel(0, 70);
        result.ShouldBe(new HexCoordinates(-1, 1));
    }

    [Fact]
    public void FromPixel_WithNegativeCoordinates_WorksCorrectly()
    {
        // Center of hex (-1, 1): H=-75, V=43.301, center = (-25, 86.603)
        var result = HexCoordinatesPixelExtensions.FromPixel(-25, 86.60254037844386);
        result.ShouldBe(new HexCoordinates(-1, 1));
    }
}
