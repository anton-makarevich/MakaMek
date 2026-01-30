using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
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

        var sut = new MovementPath(segments, MovementType.Walk);

        // Assert
        sut.Segments.Count.ShouldBe(2);
        sut.TotalCost.ShouldBe(2);
        sut.HexesTraveled.ShouldBe(1);
        sut.StraightLineDistance.ShouldBe(1);
        sut.Start!.Coordinates.ShouldBe(new HexCoordinates(1, 1));
        sut.Destination!.Coordinates.ShouldBe(new HexCoordinates(1, 2));
        sut.Destination.Facing.ShouldBe(HexDirection.TopRight);
        sut.Hexes.Count.ShouldBe(2);
        sut.TurnsTaken.ShouldBe(1);
        sut.MovementType.ShouldBe(MovementType.Walk);
    }
    
    [Fact]
    public void Constructor_ShouldThrow_WhenSegmentsEmpty()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new MovementPath(Array.Empty<PathSegment>(), MovementType.Walk));
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
        
        var originalPath = new MovementPath(segments, MovementType.Walk);
        
        // Act
        var reversedPath = originalPath.ReverseFacing();
        
        // Assert
        reversedPath.Segments.Count.ShouldBe(originalPath.Segments.Count, 
            "Reversed path should have same number of segments");
        
        // Verify all facings are opposite
        for (var i = 0; i < originalPath.Segments.Count; i++)
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
    public void HexesTraveled_ShouldNotIncludeStartHex()
    {
        // Arrange
        var segments = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 1), HexDirection.TopRight),
                1),
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.TopRight),
                new HexPosition(new HexCoordinates(2, 1), HexDirection.TopRight),
                1),
            new(
                new HexPosition(new HexCoordinates(2, 1), HexDirection.TopRight),
                new HexPosition(new HexCoordinates(2, 2), HexDirection.TopRight),
                1)
        };
        
        var sut = new MovementPath(segments, MovementType.Walk);
        
        // Assert
        sut.Hexes.Count.ShouldBe(3);   // (1,1), (2,1), (2,2)
        sut.HexesTraveled.ShouldBe(2); // (2,1) and (2,2)
    }
    
    [Fact]
    public void HexesTravelled_ShouldNotBeNegative()
    {
        // Arrange
        var sut = new MovementPath(new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                0)
        }, MovementType.StandingStill);
        
        // Assert
        sut.HexesTraveled.ShouldBe(0);
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
        
        var sut = new MovementPath(segments.Select(s => new PathSegment(s)).ToList(), MovementType.Walk);
        
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
        
        var path1 = new MovementPath(segments, MovementType.Walk);
        var path2 = new MovementPath(segments, MovementType.Walk);
        
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
        
        var path1 = new MovementPath(segments1, MovementType.Walk);
        var path2 = new MovementPath(segments2, MovementType.Walk);
        
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
        
        
        var path1 = new MovementPath(segments, MovementType.Walk);
        var path2 = new MovementPath(segments, MovementType.Jump);
        
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
        
        var path = new MovementPath(segments, MovementType.Walk);
        
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
        
        var path = new MovementPath(segments, MovementType.Walk);
        object pathAsObject = new MovementPath(path.ToData(), MovementType.Walk);
        
        // Act & Assert
        path.Equals(pathAsObject).ShouldBeTrue();
    }
    
    [Fact]
    public void CreateStandingStillPath_ShouldCreatePathWithSingleSegment()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        
        // Act
        var path = MovementPath.CreateStandingStillPath(position);
        
        // Assert
        path.Segments.Count.ShouldBe(1);
        path.Segments[0].From.ShouldBe(position);
        path.Segments[0].To.ShouldBe(position);
        path.Segments[0].Cost.ShouldBe(0);
        path.MovementType.ShouldBe(MovementType.StandingStill);
    }
    
    [Fact]
    public void Append_ShouldCombineTwoPaths()
    {
        // Arrange
        var path1 = new MovementPath([new PathSegment(new HexPosition(1, 1, HexDirection.Top), new HexPosition(1, 2, HexDirection.Top), 1)], MovementType.Walk);
        var path2 = new MovementPath([new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(1, 3, HexDirection.Top), 1)], MovementType.Walk);
        
        // Act
        var combined = path1.Append(path2);
        
        // Assert
        combined.Segments.Count.ShouldBe(2);
        combined.Segments[0].To.Coordinates.ShouldBe(new HexCoordinates(1, 2));
        combined.Segments[1].To.Coordinates.ShouldBe(new HexCoordinates(1, 3));
    }
    
    [Fact]
    public void Append_ShouldThrow_WhenMovementTypesDiffer()
    {
        // Arrange
        var path1 = new MovementPath([new PathSegment(new HexPosition(1, 1, HexDirection.Top), new HexPosition(1, 2, HexDirection.Top), 1)], MovementType.Walk);
        var path2 = new MovementPath([new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(1, 3, HexDirection.Top), 1)], MovementType.Jump);
        
        // Act & Assert
        Should.Throw<ArgumentException>(() => path1.Append(path2));
    }
    
    [Fact]
    public void Append_ShouldThrow_WhenPathsAreNotContinuous()
    {
        // Arrange
        var path1 = new MovementPath([new PathSegment(new HexPosition(1, 1, HexDirection.Top), new HexPosition(1, 2, HexDirection.Top), 1)], MovementType.Walk);
        var path2 = new MovementPath([new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(2, 3, HexDirection.Top), 1)], MovementType.Walk);
        
        // Act & Assert
        Should.Throw<ArgumentException>(() => path1.Append(path2));
    }
    
    [Fact]
    public void RemoveTrailingTurns_ShouldRemoveTurnsAtTheEndOfThePath()
    {
        // Arrange
        var segments = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom),
                1),
            new(
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.BottomLeft),
                1)
        };
        
        var path = new MovementPath(segments, MovementType.Walk);
        
        // Act
        var result = path.RemoveTrailingTurns();
        
        // Assert
        result.Segments.Count.ShouldBe(1);
        result.Segments[0].From.Coordinates.ShouldBe(new HexCoordinates(1, 1));
        result.Segments[0].To.Coordinates.ShouldBe(new HexCoordinates(1, 2));
    }
    
    [Fact]
    public void RemoveTrailingTurns_ShouldNotRemoveTurnsInMiddleOfThePath()
    {
        // Arrange
        var segments = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom),
                1),
            new(
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.BottomLeft),
                1),
            new(
                new HexPosition(new HexCoordinates(1, 2), HexDirection.BottomLeft),
                new HexPosition(new HexCoordinates(1, 3), HexDirection.BottomLeft),
                1)
        };
        
        var path = new MovementPath(segments, MovementType.Walk);
        
        // Act
        var result = path.RemoveTrailingTurns();
        
        // Assert
        result.Segments.Count.ShouldBe(3);
    }
}