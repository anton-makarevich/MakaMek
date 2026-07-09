using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

public class HexagonGeometryTests
{
    [Fact]
    public void GetCorners_ReturnsSixCorners()
    {
        var corners = HexagonGeometry.GetCorners();
        corners.Length.ShouldBe(6);
    }

    [Fact]
    public void GetCorners_ReturnsClockwiseWindingStartingFromLeft()
    {
        var corners = HexagonGeometry.GetCorners();
        // Index: 0=Left, 1=BottomLeft, 2=BottomRight, 3=Right, 4=TopRight, 5=TopLeft
        var w = HexCoordinatesPixelExtensions.HexWidth;
        var h = HexCoordinatesPixelExtensions.HexHeight;

        corners[0].X.ShouldBe(0);
        corners[0].Y.ShouldBe(h * 0.5);
        corners[1].X.ShouldBe(w * 0.25);
        corners[1].Y.ShouldBe(h);
        corners[2].X.ShouldBe(w * 0.75);
        corners[2].Y.ShouldBe(h);
        corners[3].X.ShouldBe(w);
        corners[3].Y.ShouldBe(h * 0.5);
        corners[4].X.ShouldBe(w * 0.75);
        corners[4].Y.ShouldBe(0);
        corners[5].X.ShouldBe(w * 0.25);
        corners[5].Y.ShouldBe(0);
    }

    [Theory]
    [InlineData(HexDirection.Top, 5, 4)]
    [InlineData(HexDirection.TopRight, 4, 3)]
    [InlineData(HexDirection.BottomRight, 3, 2)]
    [InlineData(HexDirection.Bottom, 2, 1)]
    [InlineData(HexDirection.BottomLeft, 1, 0)]
    [InlineData(HexDirection.TopLeft, 0, 5)]
    public void GetCorners_EachEdgeMapsToTwoAdjacentCorners(HexDirection direction, int expectedStart, int expectedEnd)
    {
        var (start, end) = direction.GetHexPointEdgeCornerIndices();
        start.ShouldBe(expectedStart);
        end.ShouldBe(expectedEnd);
    }
}
