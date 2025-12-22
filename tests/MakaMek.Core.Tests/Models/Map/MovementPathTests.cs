using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Models.Map;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Map;

public class MovementPathTests
{
    [Fact]
    public void Constructor_ShouldAcceptData_AndSetProperties()
    {
        // Arrange
        IReadOnlyList<PathSegmentData> segments =
        [
            new()
            {
                From = new HexPositionData
                {
                    Coordinates = new HexCoordinateData(1, 1),
                    Facing = 0
                },
                To = new HexPositionData
                {
                    Coordinates = new HexCoordinateData(1, 2),
                    Facing = 0
                },
                Cost = 1
            },
            new()
            {
                From = new HexPositionData
                {
                    Coordinates = new HexCoordinateData(1, 2),
                    Facing = 0
                },
                To = new HexPositionData
                {
                    Coordinates = new HexCoordinateData(1, 2),
                    Facing = 1
                },
                Cost = 1
            }
        ];

        var sut = new MovementPath(segments);

        // Assert
        sut.Segments.Count.ShouldBe(2);
        sut.TotalCost.ShouldBe(2);
        sut.HexesTraveled.ShouldBe(2);
        sut.DistanceCovered.ShouldBe(1);
        sut.Start.Coordinates.ShouldBe(new HexCoordinates(1, 1));
        sut.Destination.Coordinates.ShouldBe(new HexCoordinates(1, 2));
        sut.Destination.Facing.ShouldBe(HexDirection.TopRight);
        sut.Hexes.Count.ShouldBe(2);
        sut.TurnsTaken.ShouldBe(1);
    }
}