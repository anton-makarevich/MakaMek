using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

public class PathSegmentTests
{
    [Fact]
    public void Constructor_WithPositionsAndCost_SetsProperties()
    {
        // Arrange
        var from = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var to = new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom);
        const int cost = 3;
        const int elevationChange = 2;

        // Act
        var segment = new PathSegment(from, to, cost, ElevationChange: elevationChange);

        // Assert
        segment.From.ShouldBe(from);
        segment.To.ShouldBe(to);
        segment.Cost.ShouldBe(cost);
        segment.ElevationChange.ShouldBe(elevationChange);
    }

    [Fact]
    public void Constructor_WithData_SetsProperties()
    {
        // Arrange
        var from = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var to = new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom);
        const int cost = 3;
        const int elevationChange = -1;
        var data = new PathSegmentData
        {
            From = from.ToData(),
            To = to.ToData(),
            Cost = cost,
            ElevationChange = elevationChange
        };

        // Act
        var segment = new PathSegment(data);

        // Assert
        segment.From.ShouldBe(from);
        segment.To.ShouldBe(to);
        segment.Cost.ShouldBe(cost);
        segment.ElevationChange.ShouldBe(elevationChange);
    }

    [Fact]
    public void ToData_ReturnsCorrectData()
    {
        // Arrange
        var from = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var to = new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom);
        const int cost = 3;
        const int elevationChange = 5;
        var segment = new PathSegment(from, to, cost, ElevationChange: elevationChange);

        // Act
        var data = segment.ToData();

        // Assert
        data.From.ShouldBe(from.ToData());
        data.To.ShouldBe(to.ToData());
        data.Cost.ShouldBe(cost);
        data.ElevationChange.ShouldBe(elevationChange);
    }

    [Fact]
    public void Record_WithSameValues_AreEqual()
    {
        // Arrange
        var from = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var to = new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom);
        const int cost = 3;

        // Act
        var segment1 = new PathSegment(from, to, cost);
        var segment2 = new PathSegment(from, to, cost);

        // Assert
        segment1.ShouldBe(segment2);
    }

    [Fact]
    public void ElevationChange_Default_IsZero()
    {
        // Arrange
        var from = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var to = new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom);
        const int cost = 3;

        // Act
        var segment = new PathSegment(from, to, cost);

        // Assert
        segment.ElevationChange.ShouldBe(0);
    }
}
