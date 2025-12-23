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
        sut.Start!.Coordinates.ShouldBe(new HexCoordinates(1, 1));
        sut.Destination!.Coordinates.ShouldBe(new HexCoordinates(1, 2));
        sut.Destination.Facing.ShouldBe(HexDirection.TopRight);
        sut.Hexes.Count.ShouldBe(2);
        sut.TurnsTaken.ShouldBe(1);
    }
    
    [Fact]
    public void ReverseFacing_ShouldReverseAllFacings_AndMaintainCosts()
    {
        // Arrange - Create a path with multiple segments including turns and movement
        IReadOnlyList<PathSegment> segments =
        [
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 1), HexDirection.TopRight),
                1 // Turn cost
            ),
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.TopRight),
                new HexPosition(new HexCoordinates(2, 1), HexDirection.TopRight),
                1 // Movement cost
            ),
            new(
                new HexPosition(new HexCoordinates(2, 1), HexDirection.TopRight),
                new HexPosition(new HexCoordinates(2, 2), HexDirection.TopRight),
                2 // Movement cost 
            )
        ];
        
        var originalPath = new MovementPath(segments);
        
        // Act
        var reversedPath = originalPath.ReverseFacing();
        
        // Assert
        reversedPath.Segments.Count.ShouldBe(originalPath.Segments.Count, 
            "Reversed path should have same number of segments");
        
        // Verify all facings are opposite
        for (int i = 0; i < originalPath.Segments.Count; i++)
        {
            var original = originalPath.Segments[i];
            var reversed = reversedPath.Segments[i];
            
            reversed.From.Facing.ShouldBe(original.From.Facing.GetOppositeDirection(),
                $"Segment {i} From facing should be opposite");
            reversed.To.Facing.ShouldBe(original.To.Facing.GetOppositeDirection(),
                $"Segment {i} To facing should be opposite");
            
            // Coordinates should remain the same
            reversed.From.Coordinates.ShouldBe(original.From.Coordinates,
                $"Segment {i} From coordinates should remain the same");
            reversed.To.Coordinates.ShouldBe(original.To.Coordinates,
                $"Segment {i} To coordinates should remain the same");
            
            // Costs should remain the same
            reversed.Cost.ShouldBe(original.Cost,
                $"Segment {i} cost should remain the same");
        }
        
        // Verify the total cost is preserved
        reversedPath.TotalCost.ShouldBe(originalPath.TotalCost,
            "Total cost should be preserved");
        
        // Verify start and destination are reversed (with opposite facings)
        reversedPath.Start!.Coordinates.ShouldBe(originalPath.Start!.Coordinates);
        reversedPath.Start.Facing.ShouldBe(originalPath.Start.Facing.GetOppositeDirection());
        
        reversedPath.Destination!.Coordinates.ShouldBe(originalPath.Destination!.Coordinates);
        reversedPath.Destination.Facing.ShouldBe(originalPath.Destination.Facing.GetOppositeDirection());
    }
    
    [Fact]
    public void Destination_And_Start_ShouldBeNull_WhenCreatedWithEmptySegments()
    {
        // Arrange
        var sut = new MovementPath(Array.Empty<PathSegment>());
        
        // Assert
        sut.Start.ShouldBeNull();
        sut.Destination.ShouldBeNull();
        sut.Hexes.ShouldBeEmpty();
        sut.TurnsTaken.ShouldBe(0);
        sut.DistanceCovered.ShouldBe(0);
        sut.HexesTraveled.ShouldBe(0);
        sut.TotalCost.ShouldBe(0);
    }
    
    [Fact]
    public void ToData_ShouldConvertAllSegmentsToData()
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
        
        var sut = new MovementPath(segments.Select(s => new PathSegment(s)).ToList());
        
        // Act
        var data = sut.ToData();
        
        // Assert
        data.ShouldBe(segments);
    }
    
    [Fact]
    public void Equals_ShouldReturnTrue_WhenPathsAreIdentical()
    {
        // Arrange
        var segments = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom),
                1)
        };
        
        var path1 = new MovementPath(segments);
        var path2 = new MovementPath(segments);
        
        // Act & Assert
        path1.Equals(path2).ShouldBeTrue();
    }
    
    [Fact]
    public void Equals_ShouldReturnFalse_WhenPathsAreDifferent()
    {
        // Arrange
        var segments1 = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom),
                1)
        };
        
        var segments2 = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 3), HexDirection.Bottom),
                1)
        };
        
        var path1 = new MovementPath(segments1);
        var path2 = new MovementPath(segments2);
        
        // Act & Assert
        path1.Equals(path2).ShouldBeFalse();
    }
    
    [Fact]
    public void Equals_ShouldReturnFalse_WhenPathsAreTheSame_ButMovementTypeIsDifferent()
    {
        // Arrange
        var segments = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom),
                1)
        };
        
        
        var path1 = new MovementPath(segments);
        var path2 = new MovementPath(segments,true);
        
        // Act & Assert
        path1.Equals(path2).ShouldBeFalse();
    }
    
    [Fact]
    public void Equals_ShouldReturnFalse_WhenComparingWithRandomObject()
    {
        // Arrange
        var segments = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom),
                1)
        };
        
        var path = new MovementPath(segments);
        
        // Act & Assert
        path.Equals(new object()).ShouldBeFalse();
    }
    
    [Fact]
    public void Equals_ShouldReturnTrue_WhenComparingWithSameInstance_AsObject()
    {
        // Arrange
        var segments = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom),
                1)
        };
        
        var path = new MovementPath(segments);
        object pathAsObject = new MovementPath(path.ToData());
        
        // Act & Assert
        path.Equals(pathAsObject).ShouldBeTrue();
    }
}