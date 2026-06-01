using NSubstitute;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Shouldly;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Tests.Models;

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
                Costs = [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]
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
                Costs = [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]
            }
        ];

        var sut = new MovementPath(segments, MovementType.Walk);

        // Assert
        sut.Segments.Count.ShouldBe(2);
        sut.TotalCost.ShouldBe(2);
        sut.HexesTraveled.ShouldBe(1);
        sut.StraightLineDistance.ShouldBe(1);
        sut.Start.Coordinates.ShouldBe(new HexCoordinates(1, 1));
        sut.Destination.Coordinates.ShouldBe(new HexCoordinates(1, 2));
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
                [new RotationMovementCost
                    {
                        Value = 1,
                        FromFacing = HexDirection.Top,
                        ToFacing = HexDirection.TopLeft
                    }
                ] // Turn cost
            ),
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.TopRight),
                new HexPosition(new HexCoordinates(2, 1), HexDirection.TopRight),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }] // Movement cost
            ),
            new(
                new HexPosition(new HexCoordinates(2, 1), HexDirection.TopRight),
                new HexPosition(new HexCoordinates(2, 2), HexDirection.TopRight),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }] // Movement cost 
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
        reversedPath.Start.Coordinates.ShouldBe(originalPath.Start.Coordinates);
        reversedPath.Start.Facing.ShouldBe(originalPath.Start.Facing.GetOppositeDirection());
        
        reversedPath.Destination.Coordinates.ShouldBe(originalPath.Destination.Coordinates);
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
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]),
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.TopRight),
                new HexPosition(new HexCoordinates(2, 1), HexDirection.TopRight),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]),
            new(
                new HexPosition(new HexCoordinates(2, 1), HexDirection.TopRight),
                new HexPosition(new HexCoordinates(2, 2), HexDirection.TopRight),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
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
                [])
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
                Costs = [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]
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
                Costs = [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]
            }
        ];
        
        var sut = new MovementPath(segments.Select(s => new PathSegment(s)).ToList(), MovementType.Walk);
        
        // Act
        var data = sut.ToData();
        
        // Assert
        data.Count.ShouldBe(segments.Count);
        for (var i = 0; i < data.Count; i++)
        {
            data[i].From.ShouldBe(segments[i].From);
            data[i].To.ShouldBe(segments[i].To);
            data[i].Costs.Sum(c => c.Value).ShouldBe(segments[i].Costs.Sum(c => c.Value));
            data[i].IsReversed.ShouldBe(segments[i].IsReversed);
            data[i].ElevationChange.ShouldBe(segments[i].ElevationChange);
        }
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
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
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
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        };
        
        var segments2 = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 3), HexDirection.Bottom),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
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
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
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
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
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
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        };
        
        var path = new MovementPath(segments, MovementType.Walk);
        object pathAsObject = new MovementPath(path.ToData(), MovementType.Walk);
        
        // Act & Assert
        path.Equals(pathAsObject).ShouldBeTrue();
    }
    
    [Fact]
    public void CreateSingleSegmentPath_ShouldCreatePath_WithSingleSegment_AndDefaultMovementType()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        
        // Act
        var path = MovementPath.CreateSingleSegmentPath(position);
        
        // Assert
        path.Segments.Count.ShouldBe(1);
        path.Segments[0].From.ShouldBe(position);
        path.Segments[0].To.ShouldBe(position);
        path.Segments[0].Cost.ShouldBe(0);
        path.MovementType.ShouldBe(MovementType.StandingStill);
    }

    [Fact]
    public void CreateSingleSegmentPath_ShouldCreatePath_WithProvidedMovementType()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        const MovementType movementType = MovementType.Walk;
        
        // Act
        var path = MovementPath.CreateSingleSegmentPath(position, movementType);
        
        // Assert
        path.Segments.Count.ShouldBe(1);
        path.Segments[0].From.ShouldBe(position);
        path.Segments[0].To.ShouldBe(position);
        path.Segments[0].Cost.ShouldBe(0);
        path.MovementType.ShouldBe(movementType);
    }
    
    [Fact]
    public void Append_ShouldCombineTwoPaths()
    {
        // Arrange
        var path1 = new MovementPath([new PathSegment(new HexPosition(1, 1, HexDirection.Top), new HexPosition(1, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])], MovementType.Walk);
        var path2 = new MovementPath([new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(1, 3, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])], MovementType.Walk);
        
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
        var path1 = new MovementPath([new PathSegment(new HexPosition(1, 1, HexDirection.Top), new HexPosition(1, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])], MovementType.Walk);
        var path2 = new MovementPath([new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(1, 3, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])], MovementType.Jump);
        
        // Act & Assert
        Should.Throw<ArgumentException>(() => path1.Append(path2));
    }
    
    [Fact]
    public void Append_ShouldThrow_WhenPathsAreNotContinuous()
    {
        // Arrange
        var path1 = new MovementPath([new PathSegment(new HexPosition(1, 1, HexDirection.Top), new HexPosition(1, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])], MovementType.Walk);
        var path2 = new MovementPath([new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(2, 3, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])], MovementType.Walk);
        
        // Act & Assert
        Should.Throw<ArgumentException>(() => path1.Append(path2));
    }
    
    [Fact]
    public void Append_ShouldRemoveZeroCostSegments_WhenCurrentPathHasSingleZeroCostSegment()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var zeroCostPath = new MovementPath([new PathSegment(position, position, [])], MovementType.Walk);
        var newPath = new MovementPath([new PathSegment(position, new HexPosition(new HexCoordinates(2, 1), HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])], MovementType.Walk);
        
        // Act
        var result = zeroCostPath.Append(newPath);
        
        // Assert
        result.Segments.Count.ShouldBe(1, "Zero-cost segment should be removed, leaving only the new path's segment");
        result.Segments[0].Cost.ShouldBe(1);
        result.Segments[0].To.Coordinates.ShouldBe(new HexCoordinates(2, 1));
    }
    
    [Fact]
    public void Append_ShouldSucceed_WhenCoordinatesMatchButFacingDiffers()
    {
        // Arrange - path1 ends at (2,2,Top), path2 starts at (2,2,Bottom)
        var path1 = new MovementPath([
            new PathSegment(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(2, 2), HexDirection.Top),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])], MovementType.Walk);
        var path2 = new MovementPath([
            new PathSegment(
                new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom),
                new HexPosition(new HexCoordinates(3, 2), HexDirection.Bottom),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])], MovementType.Walk);
        
        // Act
        var combined = path1.Append(path2);
        
        // Assert
        combined.Segments.Count.ShouldBe(2);
        combined.Segments[0].To.Coordinates.ShouldBe(new HexCoordinates(2, 2));
        combined.Segments[1].To.Coordinates.ShouldBe(new HexCoordinates(3, 2));
        // Facing from first segment should be preserved
        combined.Segments[0].To.Facing.ShouldBe(HexDirection.Top);
        combined.Segments[1].From.Facing.ShouldBe(HexDirection.Bottom);
    }

    [Fact]
    public void Append_ShouldSucceed_WhenFacingChangesAtContinuationPoint()
    {
        // Arrange - simulate standup scenario where facing changes after fall
        var deployPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var path1 = new MovementPath([
            new PathSegment(
                deployPosition,
                new HexPosition(new HexCoordinates(2, 1), HexDirection.Top),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])], MovementType.Walk);
        // After fall+standup, mech faces a different direction but stays on same hex
        var path2 = new MovementPath([
            new PathSegment(
                new HexPosition(new HexCoordinates(2, 1), HexDirection.Bottom),
                new HexPosition(new HexCoordinates(3, 1), HexDirection.Bottom),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])], MovementType.Walk);
        
        // Act
        var combined = path1.Append(path2);
        
        // Assert
        combined.Segments.Count.ShouldBe(2);
        combined.Segments[1].From.Facing.ShouldBe(HexDirection.Bottom);
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
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]),
            new(
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.BottomLeft),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
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
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]),
            new(
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.BottomLeft),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]),
            new(
                new HexPosition(new HexCoordinates(1, 2), HexDirection.BottomLeft),
                new HexPosition(new HexCoordinates(1, 3), HexDirection.BottomLeft),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        };
        
        var path = new MovementPath(segments, MovementType.Walk);
        
        // Act
        var result = path.RemoveTrailingTurns();
        
        // Assert
        result.Segments.Count.ShouldBe(3);
    }
    
    [Fact]
    public void ReverseFacing_ShouldToggleIsReversed()
    {
        // Arrange
        var segments = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]) // Initially forward
        };
        
        var originalPath = new MovementPath(segments, MovementType.Walk);
        
        // Act
        var reversedPath = originalPath.ReverseFacing();
        
        // Assert
        reversedPath.Segments[0].IsReversed.ShouldBeTrue();
        
        // Act again (reverse back)
        var doubleReversed = reversedPath.ReverseFacing();
        
        // Assert
        doubleReversed.Segments[0].IsReversed.ShouldBeFalse();
    }

    [Fact]
    public void ReverseFacing_ShouldReverseRotationCostFacings()
    {
        // Arrange
        var segments = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top),
                [
                    new RotationMovementCost
                    {
                        FromFacing = HexDirection.TopRight,
                        ToFacing = HexDirection.Bottom,
                        Value = 1
                    },
                    new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }
                ])
        };

        var path = new MovementPath(segments, MovementType.Walk);

        // Act
        var reversed = path.ReverseFacing();

        // Assert
        var rotationCost = reversed.Segments[0].Costs.OfType<RotationMovementCost>().Single();
        rotationCost.FromFacing.ShouldBe(HexDirection.BottomLeft); // Opposite of TopRight
        rotationCost.ToFacing.ShouldBe(HexDirection.Top); // Opposite of Bottom

        // Non-rotation costs should be preserved as-is
        var terrainCost = reversed.Segments[0].Costs.OfType<TerrainMovementCost>().Single();
        terrainCost.Value.ShouldBe(1);
    }

    [Fact]
    public void HexesTraveled_ShouldCountOnlyLastLeg_WhenDirectionChanges()
    {
        // Arrange
        // Leg 1: Move Backward 2 hexes (Cost 2, 2 hexes traveled)
        var backwardSegments = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }],
                true),
            new(
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 3), HexDirection.Top),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }],
                true)
        };
        var backwardPath = new MovementPath(backwardSegments, MovementType.Walk);

        // Leg 2: Move Forward 1 hex (Cost 1, 1 hex traveled)
        // Starting from where a backward path ended
        var forwardSegments = new List<PathSegment> // IsReversed defaults to false
        {
            new(
                new HexPosition(new HexCoordinates(1, 3), HexDirection.Top), // Note: Facing doesn't matter for this test logic, but keeping it consistent
                new HexPosition(new HexCoordinates(1, 4), HexDirection.Top),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        };
        var forwardPath = new MovementPath(forwardSegments, MovementType.Walk);

        // Act
        var combinedPath = backwardPath.Append(forwardPath);

        // Assert
        combinedPath.Segments.Count.ShouldBe(3);
        // HexesTraveled should be 1 (only the forward leg)
        // If it counted all, it would be 3.
        combinedPath.HexesTraveled.ShouldBe(1);
    }
    
    [Fact]
    public void HexesTraveled_ShouldCountAll_WhenNoDirectionChange()
    {
        // Arrange
        var segments = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]),
            new(
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 3), HexDirection.Top),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        };
        var path = new MovementPath(segments, MovementType.Walk);

        // Assert
        path.HexesTraveled.ShouldBe(2);
    }
    
    [Fact]
    public void EventsWithLocations_ShouldReturnEmpty_WhenNoSegmentsHaveEvents()
    {
        var sut = new MovementPath(new List<PathSegment>
        {
            new(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]),
            new(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 3), HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        }, MovementType.Walk);

        var result = sut.EventsWithLocations.ToList();

        result.ShouldBeEmpty();
    }

    [Fact]
    public void EventsWithLocations_ShouldReturnEvents_WithCorrectLocations()
    {
        var fallEvent = new SegmentEvent(SegmentEventType.Fall);
        var standupEvent = new SegmentEvent(SegmentEventType.StandupAttempt);
        var sut = new MovementPath(new List<PathSegment>
        {
            new(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }], Events: [fallEvent]),
            new(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 3), HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }], Events: [standupEvent])
        }, MovementType.Walk);

        var result = sut.EventsWithLocations.ToList();

        result.Count.ShouldBe(2);
        result[0].Event.ShouldBe(fallEvent);
        result[0].Location.ShouldBe(new HexCoordinates(1, 2));
        result[1].Event.ShouldBe(standupEvent);
        result[1].Location.ShouldBe(new HexCoordinates(1, 3));
    }

    [Fact]
    public void EventsWithLocations_ShouldReturnMultipleEventsOnSameSegment_WithSameLocation()
    {
        var fall = new SegmentEvent(SegmentEventType.Fall);
        var standup = new SegmentEvent(SegmentEventType.StandupAttempt);
        var sut = new MovementPath(new List<PathSegment>
        {
            new(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }], Events: [fall, standup])
        }, MovementType.Walk);

        var result = sut.EventsWithLocations.ToList();

        result.Count.ShouldBe(2);
        result[0].Event.ShouldBe(fall);
        result[0].Location.ShouldBe(new HexCoordinates(1, 2));
        result[1].Event.ShouldBe(standup);
        result[1].Location.ShouldBe(new HexCoordinates(1, 2));
    }

    [Fact]
    public void EventsWithLocations_ShouldSkipSegments_WithoutEvents()
    {
        var fall = new SegmentEvent(SegmentEventType.Fall);
        var sut = new MovementPath(new List<PathSegment>
        {
            new(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]),
            new(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 3), HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }], Events: [fall]),
            new(new HexPosition(new HexCoordinates(1, 3), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 4), HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        }, MovementType.Walk);

        var result = sut.EventsWithLocations.ToList();

        result.Count.ShouldBe(1);
        result[0].Event.ShouldBe(fall);
        result[0].Location.ShouldBe(new HexCoordinates(1, 3));
    }

    [Fact]
    public void WithLastSegmentEvent_ShouldAppendEvent_ToLastSegment()
    {
        var sut = new MovementPath(new List<PathSegment>
        {
            new(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]),
            new(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 3), HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        }, MovementType.Walk);

        var fall = new SegmentEvent(SegmentEventType.Fall);
        var result = sut.WithLastSegmentEvent(fall);

        result.Segments[^1].Events.ShouldContain(fall);
    }

    [Fact]
    public void WithLastSegmentEvent_ShouldAppendMultipleEvents_ToLastSegment_Accumulatively()
    {
        var sut = new MovementPath(new List<PathSegment>
        {
            new(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        }, MovementType.Walk);

        var fall = new SegmentEvent(SegmentEventType.Fall);
        var standup = new SegmentEvent(SegmentEventType.StandupAttempt);
        var withFall = sut.WithLastSegmentEvent(fall);
        var withBoth = withFall.WithLastSegmentEvent(standup, new StandUpAttemptMovementCost { Value = 1 });

        withBoth.Segments[^1].Events.Length.ShouldBe(2);
        withBoth.Segments[^1].Events.ShouldContain(fall);
        withBoth.Segments[^1].Events.ShouldContain(standup);
        withBoth.Segments[^1].Costs.Count.ShouldBe(2);
        withBoth.Segments[^1].Costs.Sum(c => c.Value).ShouldBe(2);
        withBoth.TotalCost.ShouldBe(2);
    }

    [Fact]
    public void WithLastSegmentEvent_ShouldNotMutate_OriginalPath()
    {
        var sut = new MovementPath(new List<PathSegment>
        {
            new(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        }, MovementType.Walk);

        var fall = new SegmentEvent(SegmentEventType.Fall);
        sut.WithLastSegmentEvent(fall);

        sut.Segments[^1].Events.ShouldBeEmpty();
    }

    [Fact]
    public void WithLastSegmentEvent_ShouldPreservePathProperties()
    {
        var fall = new SegmentEvent(SegmentEventType.Fall);
        var original = new MovementPath(new List<PathSegment>
        {
            new(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]),
            new(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 3), HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }], Events: [new SegmentEvent(SegmentEventType.StandupAttempt)])
        }, MovementType.Walk);

        var result = original.WithLastSegmentEvent(fall);

        result.Start.ShouldBe(original.Start);
        result.Destination.ShouldBe(original.Destination);
        result.MovementType.ShouldBe(original.MovementType);
        result.Segments.Count.ShouldBe(original.Segments.Count);
        result.Hexes.ShouldBe(original.Hexes);
    }

    [Fact]
    public void WithLastSegmentEvent_ShouldIncludeEventCost_InTotalCost()
    {
        var sut = new MovementPath(new List<PathSegment>
        {
            new(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 3 }])
        }, MovementType.Walk);

        var result = sut.WithLastSegmentEvent(new SegmentEvent(SegmentEventType.StandupAttempt), new StandUpAttemptMovementCost { Value = 2 });

        result.TotalCost.ShouldBe(5);
    }

    [Fact]
    public void WithLastSegmentEvent_WithFallEvent_ShouldNotAddCost()
    {
        var sut = new MovementPath(new List<PathSegment>
        {
            new(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 3 }])
        }, MovementType.Walk);

        var result = sut.WithLastSegmentEvent(new SegmentEvent(SegmentEventType.Fall));

        result.Segments[^1].Costs.Count.ShouldBe(1);
        result.Segments[^1].Costs[0].Value.ShouldBe(3);
        result.TotalCost.ShouldBe(3);
    }

    [Fact]
    public void WithLastSegmentEvent_WithStandupEvent_ShouldAddCostToSegment()
    {
        var sut = new MovementPath(new List<PathSegment>
        {
            new(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 3 }])
        }, MovementType.Walk);

        var result = sut.WithLastSegmentEvent(new SegmentEvent(SegmentEventType.StandupAttempt), new StandUpAttemptMovementCost { Value = 2 });

        result.Segments[^1].Costs.Count.ShouldBe(2);
        result.Segments[^1].Costs.ShouldContain(c => c.Value == 2);
        result.Segments[^1].Costs.Sum(c => c.Value).ShouldBe(5);
    }

    [Fact]
    public void Render_WithNoCostSegments_ReturnsEmptyString()
    {
        var segments = new List<PathSegment>
        {
            new(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top),
                []),
            new(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 3), HexDirection.Top),
                [])
        };
        var sut = new MovementPath(segments, MovementType.Walk);
        var localization = Substitute.For<ILocalizationService>();

        var result = sut.Render(localization);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Render_WithSingleSegment_ReturnsFormattedOutput()
    {
        var segments = new List<PathSegment>
        {
            new(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }])
        };
        var sut = new MovementPath(segments, MovementType.Walk);
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_Terrain").Returns("entered {0}, {1} MP");
        localization.GetString("Terrain_Clear").Returns("Clear");

        var result = sut.Render(localization);

        result.ShouldBe($"1. 0101:0->0102:0{Environment.NewLine}- entered Clear, 2 MP");
    }

    [Fact]
    public void Render_WithMultipleSegments_ReturnsIncrementedNumbers()
    {
        var segments = new List<PathSegment>
        {
            new(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]),
            new(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 3), HexDirection.Top),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.LightWoods, Value = 2 }])
        };
        var sut = new MovementPath(segments, MovementType.Walk);
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_Terrain").Returns("entered {0}, {1} MP");
        localization.GetString("Terrain_Clear").Returns("Clear");
        localization.GetString("Terrain_LightWoods").Returns("Light Woods");

        var result = sut.Render(localization);

        result.ShouldBe(
            $"1. 0101:0->0102:0{Environment.NewLine}- entered Clear, 1 MP{Environment.NewLine}2. 0102:0->0103:0{Environment.NewLine}- entered Light Woods, 2 MP");
    }

    [Fact]
    public void Render_WithMultipleCostsOnSegment_IncludesAllCostLines()
    {
        var segments = new List<PathSegment>
        {
            new(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Top),
                [
                    new RotationMovementCost { FromFacing = HexDirection.Top, ToFacing = HexDirection.TopRight, Value = 1 },
                    new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }
                ])
        };
        var sut = new MovementPath(segments, MovementType.Walk);
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_Rotation").Returns("rotated {0} side(s), {1} MP");
        localization.GetString("MovementCost_Terrain").Returns("entered {0}, {1} MP");
        localization.GetString("Terrain_Clear").Returns("Clear");

        var result = sut.Render(localization);

        result.ShouldBe(
            $"1. 0101:0->0102:0{Environment.NewLine}- rotated 1 side(s), 1 MP{Environment.NewLine}- entered Clear, 2 MP");
    }
}