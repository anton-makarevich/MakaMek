using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class PathSegmentViewModelTests
{
    [Fact]
    public void IsTurn_ReturnsFalse_WhenPositionsAreDifferent()
    {
        // Arrange
        var from = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        var to = new HexPosition(new HexCoordinates(1, 0), HexDirection.Top);
        var segment = new PathSegment(from, to, 1);
        var sut = new PathSegmentViewModel(segment);

        // Act & Assert
        sut.IsTurn.ShouldBeFalse();
    }

    [Fact]
    public void IsTurn_ReturnsTrue_WhenSamePositionDifferentFacing()
    {
        // Arrange
        var from = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        var to = new HexPosition(new HexCoordinates(0, 0), HexDirection.TopRight);
        var segment = new PathSegment(from, to, 1);
        var sut = new PathSegmentViewModel(segment);

        // Act & Assert
        sut.IsTurn.ShouldBeTrue();
    }

    [Fact]
    public void ViewModel_ReturnsCorrectValues()
    {
        // Arrange
        var from = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var to = new HexPosition(new HexCoordinates(2, 1), HexDirection.Top);
        var segment = new PathSegment(from, to, 1);
        var sut = new PathSegmentViewModel(segment);

        // Act & Assert
        sut.From.ShouldBe(from);
        sut.To.ShouldBe(to);
        sut.FromX.ShouldBe(from.Coordinates.H);
        sut.FromY.ShouldBe(from.Coordinates.V );
    }

    [Fact]
    public void EndPoints_CalculatedCorrectly_ForStraightMovement()
    {
        // Arrange
        var from = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var to = new HexPosition(new HexCoordinates(2, 1), HexDirection.Top);
        var segment = new PathSegment(from, to, 1);
        var sut = new PathSegmentViewModel(segment);

        // Act & Assert
        sut.From.ShouldBe(from);
        sut.To.ShouldBe(to);
        sut.EndX.ShouldBe(sut.StartX + 75); // One hex to the right
        sut.EndY.ShouldBe(sut.StartY + 43.3,0.1); // Half-hex to the bottom
    }

    [Fact]
    public void EndPoints_CalculatedCorrectly_ForTurn()
    {
        // Arrange
        var from = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        var to = new HexPosition(new HexCoordinates(0, 0), HexDirection.TopRight);
        var segment = new PathSegment(from, to, 1);
        var sut = new PathSegmentViewModel(segment);
        const double turnLength = 40;

        // Expected values for 60-degree turn (NorthRight = 60 degrees)
        var expectedX = sut.StartX + turnLength * Math.Sin(Math.PI / 3); // sin(60°)
        var expectedY = sut.StartY - turnLength * Math.Cos(Math.PI / 3); // -cos(60°)

        // Act & Assert
        sut.EndX.ShouldBe(expectedX, 0.001);
        sut.EndY.ShouldBe(expectedY, 0.001);
    }

    [Fact]
    public void ArrowDirectionVector_PointsInFacingDirection_ForTurn()
    {
        // Arrange
        var from = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        var to = new HexPosition(new HexCoordinates(0, 0), HexDirection.TopRight);
        var segment = new PathSegment(from, to, 1);
        var sut = new PathSegmentViewModel(segment);

        // Expected values for NorthRight direction (60 degrees)
        var expectedX = Math.Sin(Math.PI / 3); // sin(60°)
        var expectedY = -Math.Cos(Math.PI / 3); // -cos(60°)

        // Act
        var direction = sut.ArrowDirectionVector;

        // Assert
        direction.X.ShouldBe((float)expectedX, 0.001f);
        direction.Y.ShouldBe((float)expectedY, 0.001f);
    }

    [Fact]
    public void ArrowDirectionVector_PointsInMovementDirection_ForStraightMovement()
    {
        // Arrange
        var from = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        var to = new HexPosition(new HexCoordinates(0, 1), HexDirection.Top);
        var segment = new PathSegment(from, to, 1);
        var sut = new PathSegmentViewModel(segment);

        // Expected values for North direction (0 degrees)
        var expectedX = Math.Sin(0); // sin(0°) = 0
        var expectedY = -Math.Cos(0); // -cos(0°) = -1

        // Act
        var direction = sut.ArrowDirectionVector;

        // Assert
        direction.X.ShouldBe((float)expectedX, 0.001f);
        direction.Y.ShouldBe((float)expectedY, 0.001f);
    }

    [Fact]
    public void TurnAngleSweep_Returns60_ForClockwiseTurn()
    {
        // Arrange
        var from = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        var to = new HexPosition(new HexCoordinates(0, 0), HexDirection.TopRight);
        var segment = new PathSegment(from, to, 1);
        var sut = new PathSegmentViewModel(segment);

        // Act & Assert
        sut.TurnAngleSweep.ShouldBe(60);
    }

    [Fact]
    public void TurnAngleSweep_ReturnsMinus60_ForCounterclockwiseTurn()
    {
        // Arrange
        var from = new HexPosition(new HexCoordinates(0, 0), HexDirection.TopRight);
        var to = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        var segment = new PathSegment(from, to, 1);
        var sut = new PathSegmentViewModel(segment);

        // Act & Assert
        sut.TurnAngleSweep.ShouldBe(-60);
    }

    [Fact]
    public void TurnAngleSweep_Returns0_ForStraightMovement()
    {
        // Arrange
        var from = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        var to = new HexPosition(new HexCoordinates(1, 0), HexDirection.Top);
        var segment = new PathSegment(from, to, 1);
        var sut = new PathSegmentViewModel(segment);

        // Act & Assert
        sut.TurnAngleSweep.ShouldBe(0);
    }

    [Fact]
    public void Cost_ReturnsSegmentCost()
    {
        // Arrange
        var from = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        var to = new HexPosition(new HexCoordinates(1, 0), HexDirection.Top);
        var segment = new PathSegment(from, to, 2);
        var sut = new PathSegmentViewModel(segment);

        // Act & Assert
        sut.Cost.ShouldBe(2);
    }
}
