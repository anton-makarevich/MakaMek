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

    [Fact]
    public void GetCorners_EachEdgeMapsToTwoAdjacentCorners()
    {
        var direction = HexDirection.Top;
        var (start, end) = direction.GetHexPointEdgeCornerIndices();
        // Top edge spans TopLeft→TopRight (indices 5→4)
        start.ShouldBe(5);
        end.ShouldBe(4);

        direction = HexDirection.TopRight;
        (start, end) = direction.GetHexPointEdgeCornerIndices();
        start.ShouldBe(4);
        end.ShouldBe(3);

        direction = HexDirection.BottomRight;
        (start, end) = direction.GetHexPointEdgeCornerIndices();
        start.ShouldBe(3);
        end.ShouldBe(2);

        direction = HexDirection.Bottom;
        (start, end) = direction.GetHexPointEdgeCornerIndices();
        start.ShouldBe(2);
        end.ShouldBe(1);

        direction = HexDirection.BottomLeft;
        (start, end) = direction.GetHexPointEdgeCornerIndices();
        start.ShouldBe(1);
        end.ShouldBe(0);

        direction = HexDirection.TopLeft;
        (start, end) = direction.GetHexPointEdgeCornerIndices();
        start.ShouldBe(0);
        end.ShouldBe(5);
    }
}
