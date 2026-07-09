using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

public class HexCoordinatesPixelExtensionsFromPixelTests
{
    [Fact]
    public void FromPixel_CenterOfOrigin_ReturnsOrigin()
    {
        var result = HexCoordinatesPixelExtensions.FromPixel(0, 0);
        result.ShouldBe(new HexCoordinates(0, 0));
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(75, 0, 1, 0)]
    [InlineData(150, 0, 2, 0)]
    [InlineData(0, 86.60254037844386, 0, 1)]
    [InlineData(75, 43.30127018922193, 1, 1)]
    [InlineData(150, 86.60254037844386, 2, 1)]
    [InlineData(75, -43.30127018922193, 1, 0)]
    [InlineData(0, -86.60254037844386, 0, -1)]
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
        var h = original.H;
        var v = original.V;
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
            var h = original.H;
            var v = original.V;
            var result = HexCoordinatesPixelExtensions.FromPixel(h, v);
            result.ShouldBe(original, $"Failed for ({original.Q}, {original.R}) at pixel ({h}, {v})");
        }
    }

    [Fact]
    public void FromPixel_NearEdge_ReturnsCorrectHex()
    {
        // Point at (50, 0) is the right corner of hex (0,0) — shared between
        // (0,0), (1,0), and (1,1) depending on rounding
        var result = HexCoordinatesPixelExtensions.FromPixel(50, 0);
        result.ShouldBeOneOf(new HexCoordinates(0, 0), new HexCoordinates(1, 0), new HexCoordinates(1, 1));
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
        // Point at (0, 70) — just below hex (0,0) center, should be in (0,1)
        var result = HexCoordinatesPixelExtensions.FromPixel(0, 70);
        result.ShouldBe(new HexCoordinates(0, 1));
    }

    [Fact]
    public void FromPixel_WithNegativeCoordinates_WorksCorrectly()
    {
        var result = HexCoordinatesPixelExtensions.FromPixel(-75, 43.30127018922193);
        result.ShouldBe(new HexCoordinates(-1, 1));
    }
}
