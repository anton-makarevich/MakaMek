using NSubstitute;
using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Utils.Generators;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Map;

public class BattleMapExtensionsTests
{
    [Theory]
    [InlineData(3, 3, 8)]   // 3x3 map has 8 edge hexes (perimeter)
    [InlineData(5, 5, 16)]  // 5x5 map has 16 edge hexes
    [InlineData(10, 10, 36)] // 10x10 map has 36 edge hexes
    [InlineData(1, 1, 1)]   // 1x1 map has 1 edge hex
    [InlineData(2, 2, 4)]   // 2x2 map has 4 edge hexes
    public void GetEdgeHexCoordinates_ShouldReturnCorrectNumberOfEdgeHexes(int width, int height, int expectedCount)
    {
        // Arrange
        var map = new BattleMap(width, height);
        
        // Act
        var edgeHexes = map.GetEdgeHexCoordinates();
        
        // Assert
        edgeHexes.Count.ShouldBe(expectedCount);
    }

    [Theory]
    [InlineData(1, 1, true)]   // Top-left corner
    [InlineData(5, 1, true)]   // Top-right corner
    [InlineData(1, 5, true)]   // Bottom-left corner
    [InlineData(5, 5, true)]   // Bottom-right corner
    [InlineData(3, 1, true)]   // Top edge
    [InlineData(3, 5, true)]   // Bottom edge
    [InlineData(1, 3, true)]   // Left edge
    [InlineData(5, 3, true)]   // Right edge
    [InlineData(3, 3, false)]  // Center (not edge)
    [InlineData(2, 2, false)]  // Interior (not edge)
    [InlineData(4, 4, false)]  // Interior (not edge)
    [InlineData(6, 3, false)]  // Out of bounds (not edge)
    public void GetEdgeHexCoordinates_ShouldIncludeOnlyEdgeHexes(int q, int r, bool shouldBeIncluded)
    {
        // Arrange
        var map = new BattleMap(5, 5);
        
        // Act
        var edgeHexes = map.GetEdgeHexCoordinates();
        
        // Assert
        var hex = new HexCoordinates(q, r);
        edgeHexes.Contains(hex).ShouldBe(shouldBeIncluded);
    }

    [Fact]
    public void GetEdgeHexCoordinates_AllReturnedCoordinatesShouldBeOnMapBorder()
    {
        // Arrange
        var map = new BattleMap(10, 10);
        
        // Act
        var edgeHexes = map.GetEdgeHexCoordinates();
        
        // Assert
        foreach (var hex in edgeHexes)
        {
            var isOnBorder = hex.Q == 1 || hex.Q == 10 || hex.R == 1 || hex.R == 10;
            isOnBorder.ShouldBeTrue($"Hex {hex} should be on the border");
        }
    }

    [Fact]
    public void GetEdgeHexCoordinates_ShouldNotContainDuplicates()
    {
        // Arrange
        var map = new BattleMap(5, 5);
        
        // Act
        var edgeHexes = map.GetEdgeHexCoordinates();
        
        // Assert
        var uniqueHexes = edgeHexes.Distinct().ToList();
        uniqueHexes.Count.ShouldBe(edgeHexes.Count, "Edge hexes should not contain duplicates");
    }

    [Theory]
    [InlineData(5, 5, 3, 3)]   // Odd-sized map
    [InlineData(4, 4, 2, 2)]   // Even-sized map (rounds down)
    [InlineData(10, 10, 5, 5)] // Larger even-sized map (rounds down)
    [InlineData(9, 9, 5, 5)]   // Larger odd-sized map
    [InlineData(1, 1, 1, 1)]   // Smallest map
    [InlineData(6, 8, 3, 4)]   // Rectangular map (rounds down)
    [InlineData(3, 3, 2, 2)]   // 3x3 map
    public void GetCenterHexCoordinate_ShouldReturnCorrectCenter(int width, int height, int expectedQ, int expectedR)
    {
        // Arrange
        var map = new BattleMap(width, height);
        
        // Act
        var center = map.GetCenterHexCoordinate();
        
        // Assert
        center.Q.ShouldBe(expectedQ);
        center.R.ShouldBe(expectedR);
    }

    [Fact]
    public void GetCenterHexCoordinate_ShouldUseIntegerDivision()
    {
        // Arrange - 5x5 map should have center at (3, 3) using (5+1)/2 = 3
        var map = new BattleMap(5, 5);
        
        // Act
        var center = map.GetCenterHexCoordinate();
        
        // Assert
        center.ShouldBe(new HexCoordinates(3, 3));
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(10, 10)]
    [InlineData(7, 7)]
    public void GetCenterHexCoordinate_ShouldNotBeOnEdge_ForLargerMaps(int width, int height)
    {
        // Arrange
        var map = new BattleMap(width, height);
        
        // Act
        var center = map.GetCenterHexCoordinate();
        var edgeHexes = map.GetEdgeHexCoordinates();
        
        // Assert
        edgeHexes.ShouldNotContain(center, "Center should not be on the edge for larger maps");
    }
    
    [Fact]
    public void GetReachableHexesForUnit_ShouldReturnCorrectData_ForJumpingUnit()
    {
        // Arrange
        var map = new BattleMapFactory()
            .GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var unit = Substitute.For<IUnit>();
        unit.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        unit.GetMovementPoints(MovementType.Jump).Returns(3);
        
        // Act
        var reachabilityData = map.GetReachableHexesForUnit(
            unit,
            MovementType.Jump, 
            new HashSet<HexCoordinates>(),
            new HashSet<HexCoordinates>());
        
        // Assert
        reachabilityData.ForwardReachableHexes.ShouldNotBeEmpty();
        reachabilityData.BackwardReachableHexes.ShouldBeEmpty();
        reachabilityData.AllReachableHexes.Count.ShouldBe(36);
    }
    
    [Fact]
    public void GetReachableHexesForUnit_ShouldNotIncludeFriendlyUnits()
    {
        // Arrange
        var map = new BattleMapFactory()
            .GenerateMap(10,
                10,
                new SingleTerrainGenerator(10,
                    10,
                    new ClearTerrain()));
        var unit = Substitute.For<IUnit>();
        unit.Position.Returns(new HexPosition(new HexCoordinates(5,
                5),
            HexDirection.Top));
        unit.GetMovementPoints(MovementType.Jump).Returns(3);
        HashSet<HexCoordinates> friendlyUnitsCoordinates = [new(5,
            6), new(6,
            6)];
        
        // Act
        var reachabilityData = map.GetReachableHexesForUnit(unit,
            MovementType.Jump,
            new HashSet<HexCoordinates>(),
            friendlyUnitsCoordinates);
        
        // Assert
        foreach (var hex in friendlyUnitsCoordinates)
        {
            reachabilityData.AllReachableHexes.ShouldNotContain(hex);
        }
    }
    
    [Fact]
    public void GetReachableHexesForUnit_ShouldNotIncludeProhibitedHexes()
    {
        // Arrange
        var map = new BattleMapFactory()
            .GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var unit = Substitute.For<IUnit>(); 
        unit.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        unit.GetMovementPoints(MovementType.Jump).Returns(3);
        HashSet<HexCoordinates> prohibitedHexes = [new(5, 6), new(6, 6)];
        
        // Act
        var reachabilityData = map.GetReachableHexesForUnit(unit, MovementType.Jump, prohibitedHexes,
            new HashSet<HexCoordinates>());
        
        // Assert
        foreach (var hex in prohibitedHexes)
        {
            reachabilityData.AllReachableHexes.ShouldNotContain(hex);
        }
    }
    
    [Fact]
    public void GetReachableHexesForUnit_ShouldNotIncludeStartHex()
    {
        // Arrange
        var map = new BattleMapFactory()
            .GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var unit = Substitute.For<IUnit>();
        unit.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        unit.GetMovementPoints(MovementType.Jump).Returns(3);       
        
        // Act
        var reachabilityData = map.GetReachableHexesForUnit(unit, MovementType.Jump,
            new HashSet<HexCoordinates>(),
            new HashSet<HexCoordinates>());
        
        // Assert
        reachabilityData.AllReachableHexes.ShouldNotContain(new HexCoordinates(5, 5));
    }
    
    [Fact]
    public void GetReachableHexesForUnit_ShouldNotIncludeBackwardHexes_WhenUnitCannotMoveBackward()
    {
        // Arrange
        var map = new BattleMapFactory()
            .GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var unit = Substitute.For<IUnit>();
        unit.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        unit.GetMovementPoints(MovementType.Run).Returns(3);
        unit.CanMoveBackward(MovementType.Run).Returns(false);
        
        // Act
        var reachabilityData = map.GetReachableHexesForUnit(unit, MovementType.Run, 
            new HashSet<HexCoordinates>(),
            new HashSet<HexCoordinates>());
        
        // Assert
        reachabilityData.BackwardReachableHexes.ShouldBeEmpty();
    }
    
    [Fact]
    public void GetReachableHexesForUnit_ShouldIncludeBackwardHexes_WhenUnitCanMoveBackward()
    {
        // Arrange
        var map = new BattleMapFactory()
            .GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var unit = Substitute.For<IUnit>();
        unit.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        unit.GetMovementPoints(MovementType.Walk).Returns(3);
        unit.CanMoveBackward(MovementType.Walk).Returns(true);
        
        // Act
        var reachabilityData = map.GetReachableHexesForUnit(unit, MovementType.Walk, 
            new HashSet<HexCoordinates>(),
            new HashSet<HexCoordinates>());
        
        // Assert
        reachabilityData.BackwardReachableHexes.ShouldNotBeEmpty();
    }
    
    [Fact]
    public void GetReachableHexesForUnit_ShouldReturnEmptyData_WhenUnitHasNoMovementPoints()
    {
        // Arrange
        var map = new BattleMapFactory()
            .GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var unit = Substitute.For<IUnit>();
        unit.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        unit.GetMovementPoints(MovementType.Walk).Returns(0);
        
        // Act
        var reachabilityData = map.GetReachableHexesForUnit(unit, MovementType.Walk, 
            new HashSet<HexCoordinates>(),
            new HashSet<HexCoordinates>());
        
        // Assert
        reachabilityData.AllReachableHexes.ShouldBeEmpty();
    }
    
    [Fact]
    public void GetReachableHexesForUnit_ShouldReturnEmptyData_WhenUnitHasNoPosition()
    {
        // Arrange
        var map = new BattleMapFactory()
            .GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var unit = Substitute.For<IUnit>();
        unit.Position.Returns((HexPosition?)null);

        // Act
        var reachabilityData = map.GetReachableHexesForUnit(unit, MovementType.Walk, 
            new HashSet<HexCoordinates>(),
            new HashSet<HexCoordinates>());

        // Assert
        reachabilityData.AllReachableHexes.ShouldBeEmpty();
    }

    [Fact]
    public void GetPathsToHexWithAllFacings_ShouldReturnPathsForAllDirections_WhenJumping()
    {
        // Arrange
        var map = new BattleMapFactory()
            .GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var startPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Top);
        var targetHex = new HexCoordinates(5, 7); // 2 hexes away
        var reachabilityData = new UnitReachabilityData([targetHex], []);

        // Act
        var paths = map.GetPathsToHexWithAllFacings(
            startPosition,
            targetHex,
            MovementType.Jump,
            movementPoints: 3,
            reachabilityData);

        // Assert
        paths.Count.ShouldBe(6); // Should have paths for all 6 directions
        foreach (var direction in HexDirectionExtensions.AllDirections)
        {
            paths.ShouldContainKey(direction);
            paths[direction].Destination?.Coordinates.ShouldBe(targetHex);
            paths[direction].Destination?.Facing.ShouldBe(direction);
        }
    }

    [Fact]
    public void GetPathsToHexWithAllFacings_ShouldReturnEmptyDictionary_WhenTargetTooFarForJump()
    {
        // Arrange
        var map = new BattleMapFactory()
            .GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var startPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Top);
        var targetHex = new HexCoordinates(5, 9); // 4 hexes away
        var reachabilityData = new UnitReachabilityData([targetHex], []);

        // Act
        var paths = map.GetPathsToHexWithAllFacings(
            startPosition,
            targetHex,
            MovementType.Jump,
            movementPoints: 3, // Not enough to reach
            reachabilityData);

        // Assert
        paths.ShouldBeEmpty();
    }

    [Fact]
    public void GetPathsToHexWithAllFacings_ShouldReturnPathsForForwardMovement()
    {
        // Arrange
        var map = new BattleMapFactory()
            .GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var startPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Top);
        var targetHex = new HexCoordinates(5, 7); // 2 hexes away in the forward direction
        var reachabilityData = new UnitReachabilityData([targetHex], []);

        // Act
        var paths = map.GetPathsToHexWithAllFacings(
            startPosition,
            targetHex,
            MovementType.Walk,
            movementPoints: 5,
            reachabilityData);

        // Assert
        paths.ShouldNotBeEmpty();
        foreach (var path in paths.Values)
        {
            path.Destination?.Coordinates.ShouldBe(targetHex);
        }
    }

    [Fact]
    public void GetPathsToHexWithAllFacings_ShouldReturnPathsForBackwardMovement()
    {
        // Arrange
        var map = new BattleMapFactory()
            .GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var startPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Top);
        var targetHex = new HexCoordinates(5, 3); // 2 hexes away in the backward direction
        var reachabilityData = new UnitReachabilityData([], [targetHex]);

        // Act
        var paths = map.GetPathsToHexWithAllFacings(
            startPosition,
            targetHex,
            MovementType.Walk,
            movementPoints: 5,
            reachabilityData);

        // Assert
        paths.ShouldNotBeEmpty();
        foreach (var path in paths.Values)
        {
            path.Destination?.Coordinates.ShouldBe(targetHex);
        }
    }

    [Fact]
    public void GetPathsToHexWithAllFacings_ShouldReturnEmptyDictionary_WhenNeitherForwardNorBackward()
    {
        // Arrange
        var map = new BattleMapFactory()
            .GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var startPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Top);
        var targetHex = new HexCoordinates(5, 7);
        var reachabilityData = new UnitReachabilityData([], []);

        // Act
        var paths = map.GetPathsToHexWithAllFacings(
            startPosition,
            targetHex,
            MovementType.Walk,
            movementPoints: 5,
            reachabilityData);

        // Assert
        paths.ShouldBeEmpty();
    }

    [Fact]
    public void GetPathsToHexWithAllFacings_ShouldHandleZeroLengthPath_WhenTargetIsSameHex()
    {
        // Arrange
        var map = new BattleMapFactory()
            .GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var startPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Top);
        var targetHex = new HexCoordinates(5, 5); // Same hex
        var reachabilityData = new UnitReachabilityData([targetHex], []);

        // Act
        var paths = map.GetPathsToHexWithAllFacings(
            startPosition,
            targetHex,
            MovementType.Walk,
            movementPoints: 5,
            reachabilityData);

        // Assert
        paths.ShouldNotBeEmpty();

        // The path for the same facing should have 0 segments
        paths[HexDirection.Top].Segments.Count.ShouldBe(0);

        // Paths for other facings should have turning segments
        paths[HexDirection.TopRight].Segments.Count.ShouldBe(1);
        paths[HexDirection.TopRight].TurnsTaken.ShouldBe(1);
    }

    [Fact]
    public void GetPathsToHexWithAllFacings_ShouldRespectProhibitedHexes()
    {
        // Arrange
        var map = new BattleMapFactory()
            .GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var startPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Top);
        var targetHex = new HexCoordinates(5, 7);
        var prohibitedHexes = new HashSet<HexCoordinates> { new(5, 6) }; // Block the direct path
        var reachabilityData = new UnitReachabilityData([targetHex], []);

        // Act
        var paths = map.GetPathsToHexWithAllFacings(
            startPosition,
            targetHex,
            MovementType.Walk,
            movementPoints: 5,
            reachabilityData,
            prohibitedHexes);

        // Assert - Should either find alternate paths or return empty if no path exists
        // The exact behavior depends on the pathfinding algorithm
        foreach (var path in paths.Values)
        {
            // Verify that prohibited hexes are not in the path
            foreach (var hex in path.Hexes)
            {
                prohibitedHexes.ShouldNotContain(hex);
            }
        }
    }
}
