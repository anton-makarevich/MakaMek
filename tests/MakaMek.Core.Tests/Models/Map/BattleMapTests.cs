using System.Reflection;
using Shouldly;
using Sanet.MakaMek.Core.Exceptions;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Utils.Generators;

namespace Sanet.MakaMek.Core.Tests.Models.Map;

public class BattleMapTests
{
    public static readonly IBattleMapFactory BattleMapFactory = new BattleMapFactory();
    [Fact]
    public void Constructor_SetsWidthAndHeight()
    {
        // Arrange & Act
        const int width = 5;
        const int height = 4;
        var map = new BattleMap(width, height);

        // Assert
        map.Width.ShouldBe(width);
        map.Height.ShouldBe(height);
    }

    [Fact]
    public void AddHex_StoresHexInMap()
    {
        // Arrange
        var map = new BattleMap(1, 1);
        var hex = new Hex(new HexCoordinates(1, 1));

        // Act
        map.AddHex(hex);

        // Assert
        map.GetHex(hex.Coordinates).ShouldBe(hex);
    }

    [Theory]
    [InlineData(-1, 0)]  // Left of the map
    [InlineData(2, 0)]   // Right of the map
    [InlineData(0, -1)]  // Above the map
    [InlineData(0, 2)]   // Below the map
    public void AddHex_OutsideMapBoundaries_ThrowsException(int q, int r)
    {
        // Arrange
        var map = new BattleMap(2, 2);
        var hex = new Hex(new HexCoordinates(q, r));

        // Act & Assert
        var ex =Should.Throw<HexOutsideOfMapBoundariesException>(()=>map.AddHex(hex));
        ex.Coordinates.ShouldBe(hex.Coordinates);
        ex.MapWidth.ShouldBe(2); 
        ex.MapHeight.ShouldBe(2);
    }

    [Fact]
    public void FindPath_WithFacingChanges_ConsidersTurningCost()
    {
        // Arrange
        var map = new BattleMap(3, 1);
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(3, 1), HexDirection.Bottom);

        // Add hexes with clear terrain
        for (var q = 1; q <= 3; q++)
        {
            var hex = new Hex(new HexCoordinates(q, 1));
            hex.AddTerrain(new ClearTerrain());
            map.AddHex(hex);
        }

        // Act
        var path = map.FindPath(start, target, MovementType.Walk, 10);

        // Assert
        path.ShouldNotBeNull();
        path.Segments.Count.ShouldBe(7); // Should include direction changes
        path.Segments.Select(p => (p.To.Coordinates, p.To.Facing)).ShouldBe([
            (new HexCoordinates(1, 1), HexDirection.TopRight),
            (new HexCoordinates(1, 1), HexDirection.BottomRight),
            (new HexCoordinates(2, 1), HexDirection.BottomRight),
            (new HexCoordinates(2, 1), HexDirection.TopRight),
            (new HexCoordinates(3, 1), HexDirection.TopRight),
            (new HexCoordinates(3, 1), HexDirection.BottomRight),
            (new HexCoordinates(3, 1), HexDirection.Bottom)
        ]);
    }

    [Fact]
    public void GetReachableHexes_WithClearTerrain_ReturnsCorrectHexes()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5,
            new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(3, 3), HexDirection.Top);

        // Act
        var reachable = map.GetReachableHexes(start, 2).ToList();

        // Assert
        reachable.Count.ShouldBe(5); // 
        reachable.All(h => h.cost <= 2).ShouldBeTrue();
        reachable.Count(h => h.cost == 1).ShouldBe(1); // 6 adjacent hexes
        reachable.Count(h => h.cost == 2).ShouldBe(3); // 12 hexes at distance 2
    }

    [Fact]
    public void GetReachableHexes_WithMixedTerrain_ConsidersTerrainCosts()
    {
        // Arrange
        var map = new BattleMap(2, 2);
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);

        // Add clear terrain hex
        var clearHex = new Hex(new HexCoordinates(2, 1));
        clearHex.AddTerrain(new ClearTerrain());
        map.AddHex(clearHex);

        // Add heavy woods hex
        var woodsHex = new Hex(new HexCoordinates(1, 2));
        woodsHex.AddTerrain(new HeavyWoodsTerrain());
        map.AddHex(woodsHex);

        // Act
        var reachable = map.GetReachableHexes(start, 2).ToList();

        // Assert
        reachable.Count.ShouldBe(2); // Only the clear hex should be reachable
        reachable.Last().coordinates.ShouldBe(clearHex.Coordinates);
    }

    [Fact]
    public void HasLineOfSight_WithClearPath_ReturnsTrue()
    {
        // Arrange
        var map = new BattleMap(4, 1);
        var start = new HexCoordinates(1, 1);
        var end = new HexCoordinates(4, 1);

        // Add clear terrain hexes
        for (var q = 1; q <= 4; q++)
        {
            var hex = new Hex(new HexCoordinates(q, 1));
            hex.AddTerrain(new ClearTerrain());
            map.AddHex(hex);
        }

        // Act
        var hasLos = map.HasLineOfSight(start, end);

        // Assert
        hasLos.ShouldBeTrue();
    }

    [Fact]
    public void HasLineOfSight_WithBlockingTerrain_ReturnsFalse()
    {
        // Arrange
        var map = new BattleMap(4, 1);
        var start = new HexCoordinates(1, 1);
        var end = new HexCoordinates(4, 1);

        // Add clear terrain hexes
        for (var q = 1; q <= 4; q++)
        {
            var hex = new Hex(new HexCoordinates(q, 1));
            hex.AddTerrain(new ClearTerrain());
            map.AddHex(hex);
        }

        // Add blocking heavy woods in the middle
        var blockingHex = new Hex(new HexCoordinates(2, 1), 2); // Higher base level
        blockingHex.AddTerrain(new HeavyWoodsTerrain()); // Adds 2 more levels
        map.AddHex(blockingHex);

        // Act
        var hasLos = map.HasLineOfSight(start, end);

        // Assert
        hasLos.ShouldBeFalse();
    }

    [Fact]
    public void GetReachableHexes_WithComplexTerrainAndFacing_ReachesHexThroughClearPath()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(11, 9,
            new SingleTerrainGenerator(11, 9, new ClearTerrain())); // Size to fit all hexes (0-10, 0-8)

        // Heavy Woods
        var heavyWoodsCoords = new[]
        {
            (4, 7), (5, 7), (6, 8),
            (8, 3), (9, 3), (8, 4), (9, 4),
            (9, 8), (10, 6)
        };
        foreach (var (q, r) in heavyWoodsCoords)
        {
            var hex = map.GetHex(new HexCoordinates(q, r));
            hex!.RemoveTerrain(MakaMekTerrains.Clear);
            hex.AddTerrain(new HeavyWoodsTerrain());
        }

        // Light Woods
        var lightWoodsCoords = new[]
        {
            (4, 6), (9, 6), (9, 7), (10, 7)
        };
        foreach (var (q, r) in lightWoodsCoords)
        {
            var hex = map.GetHex(new HexCoordinates(q, r));
            hex!.RemoveTerrain(MakaMekTerrains.Clear);
            hex.AddTerrain(new LightWoodsTerrain());
        }

        // Starting position: 9,5 facing bottom-left (direction 4)
        var start = new HexPosition(new HexCoordinates(9, 5), HexDirection.BottomLeft);
        const int maxMp = 5;

        // Act
        var reachableHexes = map.GetReachableHexes(start, maxMp).ToList();

        // Assert
        var targetHex = new HexCoordinates(7, 8);
        reachableHexes.ShouldContain(x => x.coordinates == targetHex,
            "Hex (7,8) should be reachable through path: (9,5)->(8,5)->(7,6)->[turn]->(7,7)->(7,8)");

        // Verify the path exists and respects movement points
        var path = map.FindPath(
            start,
            new HexPosition(targetHex, HexDirection.Bottom),
            MovementType.Walk,
            maxMp);

        path.ShouldNotBeNull("A valid path should exist to reach (7,8)");

        path.Segments.Count.ShouldBeLessThanOrEqualTo(maxMp + 1,
            "Path length should not exceed maxMP + 1 (including start position)");

        var pathCoords = path.Segments.Select(p => p.To.Coordinates).Distinct().ToList();
        pathCoords.ShouldContain(new HexCoordinates(8, 5), "Path should go through (8,5)");
        pathCoords.ShouldContain(new HexCoordinates(7, 6), "Path should go through (7,6)");
        pathCoords.ShouldContain(new HexCoordinates(7, 7), "Path should go through (7,7)");
        pathCoords.ShouldContain(targetHex, "Path should reach (7,8)");
    }

    [Fact]
    public void GetReachableHexes_WithProhibitedHexes_ExcludesProhibitedHexes()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(3, 3,
            new SingleTerrainGenerator(3,3, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(2, 2), HexDirection.Top);

        // Create prohibited hexes - block two adjacent hexes
        var prohibitedHexes = new HashSet<HexCoordinates>
        {
            new(2, 1), // Hex above start
            new(3, 2)  // Hex to the right of start
        };

        // Act
        var reachable = map.GetReachableHexes(start, 2, prohibitedHexes).ToList();

        // Assert
        reachable.ShouldNotBeEmpty("Some hexes should be reachable");
        reachable.ShouldNotContain(h => prohibitedHexes.Contains(h.coordinates), 
            "Prohibited hexes should not be included in reachable hexes");
    }

    [Fact]
    public void FindPath_WithProhibitedHexes_FindsAlternativePath()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(3, 3,
            new SingleTerrainGenerator(3,3, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var target = new HexPosition(new HexCoordinates(3, 3), HexDirection.Bottom);
        
        // Create prohibited hexes that block the direct path
        var prohibitedHexes = new HashSet<HexCoordinates>
        {
            new(2, 2),
            new(3, 2)
        };

        // Act
        var path = map.FindPath(start, target, MovementType.Walk, 10, prohibitedHexes);

        // Assert
        path.ShouldNotBeNull();
        var pathCoordinates = path.Segments.Select(p => p.To.Coordinates).ToList();
        foreach (var prohibitedHex in prohibitedHexes)
        {
            pathCoordinates.ShouldNotContain(prohibitedHex);
        }
        pathCoordinates.ShouldContain(new HexCoordinates(1, 2)); // Should go around through the left side
        pathCoordinates.ShouldContain(new HexCoordinates(2, 3));
    }

    [Fact]
    public void FindPath_WithTerrainCosts_ShouldConsiderMovementCosts()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(2, 5, 
            new SingleTerrainGenerator(2,5, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var target = new HexPosition(new HexCoordinates(1, 5), HexDirection.Bottom);

        // Add two possible paths:
        // Path 1 (direct but costly): Through heavy woods (1,1)->(1,2)->(1,3)->(1,4)->(1,5)
        //   Cost: 3+3+3+1 = 10 MP (each heavy woods hex costs 3)
        // Path 2 (longer but cheaper): Around through clear terrain (1,1)->(2,1)->(2,2)->(2,3)->(2,4)->(1,5)
        //   Cost: 1+1+1+1+1 = 5 MP (clear terrain) + 4 MP (direction changes) = 9 MP total

        // Add heavy woods on the direct path
        var woodsHexes = new[]
        {
            new HexCoordinates(1, 2),
            new HexCoordinates(1, 3),
            new HexCoordinates(1, 4)
        };

        foreach (var coord in woodsHexes)
        {
            var hex = new Hex(coord);
            hex.AddTerrain(new HeavyWoodsTerrain()); // Movement cost 3
            map.AddHex(hex);
        }

        // Act
        var path = map.FindPath(start, target, MovementType.Walk, 9);

        // Assert
        path.ShouldNotBeNull("A path should exist within 9 movement points");
        
        // The path should go through clear terrain to avoid heavy woods
        var pathCoords = path.Segments.Select(p => p.To.Coordinates).Distinct().ToList();
        pathCoords.ShouldContain(new HexCoordinates(2, 1), "Path should go through clear terrain at (2,1)");
        pathCoords.ShouldContain(new HexCoordinates(2, 2), "Path should go through clear terrain at (2,2)");
        pathCoords.ShouldContain(new HexCoordinates(2, 3), "Path should go through clear terrain at (2,3)");
        pathCoords.ShouldContain(new HexCoordinates(2, 4), "Path should go through clear terrain at (2,4)");
        woodsHexes.ShouldNotContain(coord => pathCoords.Contains(coord), 
            "Path should avoid all heavy woods hexes");

        // Verify path costs
        var totalCost = path.Segments.Sum(s => s.Cost);
        totalCost.ShouldBe(9, "Total path cost should be 9 MP (5 MP for movement + 4 MP for turns)");
        
        // Verify movement costs
        var movementSegments = path.Segments.Where(s => s.From.Coordinates != s.To.Coordinates).ToList();
        movementSegments.ShouldAllBe(s => s.Cost == 1, "All movement segments should cost 1 MP as they go through clear terrain");
        
        // Verify turning costs
        var turnSegments = path.Segments.Where(s => s.From.Coordinates == s.To.Coordinates).ToList();
        turnSegments.Count.ShouldBe(4, "Should have 4 turns");
        turnSegments.ShouldAllBe(s => s.Cost==1, 
            "All turn segments should cost 1 MP");
    }

    [Theory]
    [InlineData(typeof(ClearTerrain))] 
    [InlineData(typeof(LightWoodsTerrain))] 
    [InlineData(typeof(HeavyWoodsTerrain))] 
    public void GetJumpReachableHexes_WithDifferentTerrains_IgnoresTerrainCost(Type terrainType)
    {
        // Arrange
        var terrain = (Terrain)Activator.CreateInstance(terrainType)!;
        var map = BattleMapFactory.GenerateMap(5, 5,
            new SingleTerrainGenerator(5, 5, terrain));
        var start = new HexCoordinates(3, 3);
        const int movementPoints = 2; 

        // Act
        var reachableHexes = map.GetJumpReachableHexes(start, movementPoints).ToList();

        // Assert
        reachableHexes.Count.ShouldBe(18, 
            $"Should have 18 total reachable hexes with {terrainType.Name}");
        reachableHexes.ShouldNotContain(start, 
            "Should not include start hex");
        reachableHexes.All(h => h.DistanceTo(start) <= movementPoints).ShouldBeTrue(
            "All hexes should be within movement range");
        
        // Verify we have the correct number of hexes at each distance
        reachableHexes.Count(h => h.DistanceTo(start) == 1).ShouldBe(6, 
            "Should have 6 hexes at distance 1");
        reachableHexes.Count(h => h.DistanceTo(start) == 2).ShouldBe(12, 
            "Should have 12 hexes at distance 2");
    }

    [Fact]
    public void GetJumpReachableHexes_WithProhibitedHexes_ExcludesProhibitedHexes()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5,
            new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var start = new HexCoordinates(3, 3);
        const int movementPoints = 2;

        // Prohibit some adjacent hexes
        var prohibitedHexes = start.GetAdjacentCoordinates().Take(3).ToHashSet();

        // Act
        var reachableHexes = map.GetJumpReachableHexes(start, movementPoints, prohibitedHexes).ToList();

        // Assert
        foreach (var coordinates in prohibitedHexes)
        {
            reachableHexes.ShouldNotContain(coordinates);
        }

        reachableHexes.All(h => h.DistanceTo(start) <= movementPoints).ShouldBeTrue();
    }

    [Fact]
    public void GetJumpReachableHexes_AtMapEdge_ReturnsOnlyValidHexes()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(3, 3,
            new SingleTerrainGenerator(3, 3, new ClearTerrain()));
        var start = new HexCoordinates(1, 1); // Corner hex
        const int movementPoints = 2;

        // Act
        var reachableHexes = map.GetJumpReachableHexes(start, movementPoints).ToList();

        // Assert
        reachableHexes.ShouldAllBe(h => 
                h.Q >= 1 && h.Q <= 3 && 
                h.R >= 1 && h.R <= 3, 
            "All hexes should be within map boundaries (Q: 1-3, R: 1-3)");

        reachableHexes.ShouldAllBe(h => 
                h.DistanceTo(start) <= movementPoints,
            $"All hexes should be within {movementPoints} movement points from start");
    }

    [Theory]
    [InlineData(1, 1, 1, 2, 1, true)]  // Adjacent hex, within range
    [InlineData(1, 1, 1, 3, 2, true)]  // Two hexes away, within range
    [InlineData(1, 1, 1, 4, 2, false)] // Three hexes away, out of range
    public void FindJumpPath_ReturnsCorrectPath(int fromQ, int fromR, int toQ, int toR, int mp, bool shouldFindPath)
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new HeavyWoodsTerrain()));
        var from = new HexPosition(new HexCoordinates(fromQ, fromR), HexDirection.Top);
        var to = new HexPosition(new HexCoordinates(toQ, toR), HexDirection.Bottom);

        // Act
        var path = map.FindPath(from, to, MovementType.Jump, mp);

        // Assert
        if (shouldFindPath)
        {
            path.ShouldNotBeNull("Path should be found within movement points");
            path.Segments.Count.ShouldBe(from.Coordinates.DistanceTo(to.Coordinates),
                "Path should have one segment per hex traversed");
            
            // Verify each segment costs 1 MP
            foreach (var pathSegment in path.Segments)
            {
                pathSegment.Cost.ShouldBe(1);
            }
            // Verify the path leads to the target
            path.Segments[^1].To.Coordinates.ShouldBe(to.Coordinates,
                "Path should end at target coordinates");
            path.Segments[^1].To.Facing.ShouldBe(to.Facing,
                "Path should end with target facing");
        }
        else
        {
            path.ShouldBeNull("Path should not be found when target is out of range");
        }
    }

    [Fact]
    public void FindJumpPath_IgnoresTerrainCosts()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new HeavyWoodsTerrain()));
        var from = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var to = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);

        // Act
        var path = map.FindPath(from, to, MovementType.Jump, 1);

        // Assert
        path.ShouldNotBeNull("Path should be found regardless of terrain");
        path.Segments.Count.ShouldBe(1, "Path should have one segment for adjacent hex");
        path.Segments[0].Cost.ShouldBe(1, "Cost should be 1 regardless of terrain");
    }

    [Fact]
    public void FindJumpPath_ReturnsNullForInvalidPositions()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain()));
        var from = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var invalidTo = new HexPosition(new HexCoordinates(3, 3), HexDirection.Bottom);

        // Act
        var path = map.FindPath(from, invalidTo, MovementType.Jump, 5);

        // Assert
        path.ShouldBeNull("Path should be null for invalid target position");
    }
    
    [Fact]
    public void IsOnMap_ReturnsTrueForValidCoordinates()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5,
            new SingleTerrainGenerator(5, 5, new ClearTerrain()));

        // Act
        var isOnMap = map.IsOnMap(new HexCoordinates(3, 3));

        // Assert
        isOnMap.ShouldBeTrue();
    }
    
    [Fact]
    public void IsOnMap_ReturnsFalseForInvalidCoordinates()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5,
            new SingleTerrainGenerator(5, 5, new ClearTerrain()));

        // Act
        var isOnMap = map.IsOnMap(new HexCoordinates(6, 6));

        // Assert
        isOnMap.ShouldBeFalse();
    }

    [Fact]
    public void FindPath_SameHex_ReturnsOnlyTurningSegments()
    {
        // Arrange
        var map = new BattleMap(2, 2);
        var hex = new Hex(new HexCoordinates(1, 1));
        map.AddHex(hex);
        var start = new HexPosition(hex.Coordinates, HexDirection.Top);
        var target = new HexPosition(hex.Coordinates, HexDirection.Bottom);

        // Act
        var path = map.FindPath(start, target, MovementType.Walk, 3);

        // Assert
        path.ShouldNotBeNull();
        path.Segments.Count.ShouldBe(3); // Should take 3 turns to rotate 180 degrees
        path.Segments.All(segment => segment.From.Coordinates == hex.Coordinates).ShouldBeTrue(); // All segments in the same hex
        path.Segments.All(segment => segment.To.Coordinates == hex.Coordinates).ShouldBeTrue();
        path.Segments.All(segment => segment.Cost == 1).ShouldBeTrue(); // Each turn costs 1
        path.Segments[^1].To.Facing.ShouldBe(HexDirection.Bottom); // Should end facing the target direction
    }

    [Fact]
    public void FindPath_SameHex_ExceedingMovementPoints_ReturnsNull()
    {
        // Arrange
        var map = new BattleMap(2, 2);
        var hex = new Hex(new HexCoordinates(1, 1));
        map.AddHex(hex);
        var start = new HexPosition(hex.Coordinates, HexDirection.Top);
        var target = new HexPosition(hex.Coordinates, HexDirection.Bottom);

        // Act
        var path = map.FindPath(start, target, MovementType.Walk, 2); // Only 2 movement points for 3 turns

        // Assert
        path.ShouldBeNull();
    }

    [Fact]
    public void FindPath_SameHex_NoTurningNeeded_ReturnsOneSegment_WithNoHexes_AndNoTurns()
    {
        // Arrange
        var map = new BattleMap(2, 2);
        var hex = new Hex(new HexCoordinates(1, 1));
        map.AddHex(hex);
        var position = new HexPosition(hex.Coordinates, HexDirection.Top);

        // Act
        var path = map.FindPath(position, position, MovementType.Jump, 3);

        // Assert
        path.ShouldNotBeNull();
        path.Segments.Count.ShouldBe(1);
        path.Hexes.Count.ShouldBe(1);
        path.HexesTraveled.ShouldBe(0);
        path.TurnsTaken.ShouldBe(0);
        path.TotalCost.ShouldBe(0);
    }

    [Fact]
    public void HasLineOfSight_ReturnsFalse_WhenCoordinatesAreInvalid()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var invalidCoord = new HexCoordinates(0, 0);
        var validCoord = new HexCoordinates(1, 1);

        // Act & Assert
        map.HasLineOfSight(invalidCoord, validCoord).ShouldBeFalse();
        map.HasLineOfSight(validCoord, invalidCoord).ShouldBeFalse();
    }

    [Fact]
    public void HasLineOfSight_ReturnsTrue_WhenSameHex()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var coord = new HexCoordinates(1, 1);

        // Act & Assert
        map.HasLineOfSight(coord, coord).ShouldBeTrue();
    }

    [Fact]
    public void HasLineOfSight_ReturnsTrue_WhenNoInterveningTerrain()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(1, 3);

        // Act & Assert
        map.HasLineOfSight(from, to).ShouldBeTrue();
    }
    
    [Fact]
    public void HasLineOfSight_ReturnsTrue_WhenAdjacentHexes()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(1, 2);

        // Act & Assert
        map.HasLineOfSight(from, to).ShouldBeTrue();
    }

    [Fact]
    public void HasLineOfSight_ReturnsTrue_WhenInterveningFactorLessThanThree()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(1, 4);
        
        // Set two light woods (factor 1 each) in between
        var hex1 = map.GetHex(new HexCoordinates(1, 2))!;
        hex1.RemoveTerrain(MakaMekTerrains.Clear);
        hex1.AddTerrain(new LightWoodsTerrain());
        
        var hex2 = map.GetHex(new HexCoordinates(1, 3))!;
        hex2.RemoveTerrain(MakaMekTerrains.Clear);
        hex2.AddTerrain(new LightWoodsTerrain());

        // Act & Assert
        map.HasLineOfSight(from, to).ShouldBeTrue();
    }

    [Fact]
    public void HasLineOfSight_ReturnsFalse_WhenInterveningFactorEqualsThree()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(1, 4);
        
        // Set terrain with a total factor of 3 (HeavyWoods=2, LightWoods=1)
        var hex1 = map.GetHex(new HexCoordinates(1, 2))!;
        hex1.RemoveTerrain(MakaMekTerrains.Clear);
        hex1.AddTerrain(new HeavyWoodsTerrain());
        
        var hex2 = map.GetHex(new HexCoordinates(1, 3))!;
        hex2.RemoveTerrain(MakaMekTerrains.Clear);
        hex2.AddTerrain(new LightWoodsTerrain());

        // Act & Assert
        map.HasLineOfSight(from, to).ShouldBeFalse();
    }

    [Fact]
    public void HasLineOfSight_ReturnsFalse_WhenInterveningFactorGreaterThanThree()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(1, 4);
        
        // Set terrain with a total factor of 4 (HeavyWoods=2 each)
        var hex1 = map.GetHex(new HexCoordinates(1, 2))!;
        hex1.RemoveTerrain(MakaMekTerrains.Clear);
        hex1.AddTerrain(new HeavyWoodsTerrain());
        
        var hex2 = map.GetHex(new HexCoordinates(1, 3))!;
        hex2.RemoveTerrain(MakaMekTerrains.Clear);
        hex2.AddTerrain(new HeavyWoodsTerrain());

        // Act & Assert
        map.HasLineOfSight(from, to).ShouldBeFalse();
    }

    [Fact]
    public void HasLineOfSight_IgnoresStartAndEndHexTerrain()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(1, 3);
        
        // Set HeavyWoods (factor 2) at start and end - should be ignored
        var fromHex = map.GetHex(from)!;
        fromHex.RemoveTerrain(MakaMekTerrains.Clear);
        fromHex.AddTerrain(new HeavyWoodsTerrain());
        
        var toHex = map.GetHex(to)!;
        toHex.RemoveTerrain(MakaMekTerrains.Clear);
        toHex.AddTerrain(new HeavyWoodsTerrain());
        
        // Set LightWoods (factor 1) in between - not enough to block LOS
        var middleHex = map.GetHex(new HexCoordinates(1, 2))!;
        middleHex.RemoveTerrain(MakaMekTerrains.Clear);
        middleHex.AddTerrain(new LightWoodsTerrain());

        // Act & Assert
        map.HasLineOfSight(from, to).ShouldBeTrue();
    }

    [Fact]
    public void HasLineOfSight_WithHeavyWoodsCluster_ShouldBlockLOS()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var attacker = new HexCoordinates(2, 3);
        var target = new HexCoordinates(7, 3);
        
        // Set up heavy woods cluster
        var heavyWoodsCoords = new[]
        {
            new HexCoordinates(3, 3),
            new HexCoordinates(3, 4),
            new HexCoordinates(4, 3)
        };

        foreach (var coord in heavyWoodsCoords)
        {
            var hex = map.GetHex(coord)!;
            hex.RemoveTerrain(MakaMekTerrains.Clear);
            hex.AddTerrain(new HeavyWoodsTerrain());
        }

        // Act
        var hasLos = map.HasLineOfSight(attacker, target);

        // Assert
        // LOS should be blocked because the line passes through multiple heavy woods hexes
        // Each heavy woods has an intervening factor of 2, and total intervening factor >= 3 blocks LOS
        hasLos.ShouldBeFalse($"LOS should be blocked by heavy woods cluster between {attacker} and {target}");
    }

    [Fact]
    public void HasLineOfSight_DividedLine_ShouldPreferDefenderOption()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var start = new HexCoordinates(2, 2);
        var end = new HexCoordinates(6, 2);
        
        // Add heavy forest to (3,2) and (4,2)
        map.GetHex(new HexCoordinates(3, 2))!.AddTerrain(new HeavyWoodsTerrain());
        map.GetHex(new HexCoordinates(4, 2))!.AddTerrain(new HeavyWoodsTerrain());

        // Act & Assert
        map.HasLineOfSight(start, end).ShouldBeFalse(
            "LOS should be blocked because the defender's path through (3,2) with heavy forest is preferred");
    }
    
    [Fact]
    public void HasLineOfSight_DividedLine_ShouldPreferDefenderSecondaryOption()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(10, 10, 
            new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var start = new HexCoordinates(2, 2);
        var end = new HexCoordinates(6, 2);
        
        // Add heavy forest to (3,2) and (4,2)
        map.GetHex(new HexCoordinates(3, 3))!.AddTerrain(new HeavyWoodsTerrain());
        map.GetHex(new HexCoordinates(4, 2))!.AddTerrain(new HeavyWoodsTerrain());

        // Act & Assert
        map.HasLineOfSight(start, end).ShouldBeFalse(
            "LOS should be blocked because the defender's path through (3,2) with heavy forest is preferred");
    }

    [Fact]
    public void HasLineOfSight_CacheCleared_ShouldRecalculatePath()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(10, 10, 
            new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var from = new HexCoordinates(2, 2);
        var to = new HexCoordinates(6, 2);
        
        // Initial LOS with no obstacles
        var initialLos = map.HasLineOfSight(from, to);
        initialLos.ShouldBeTrue("Should have LOS with no obstacles");

        // Add heavy forest to block LOS
        map.GetHex(new HexCoordinates(3, 3))!.AddTerrain(new HeavyWoodsTerrain());
        map.GetHex(new HexCoordinates(4, 2))!.AddTerrain(new HeavyWoodsTerrain());

        // Clear cache to force recalculation
        map.ClearLosCache();

        // Act & Assert
        var losAfterChange = map.HasLineOfSight(from, to);
        losAfterChange.ShouldBeFalse("LOS should be blocked after adding forest and clearing cache");
    }

    [Fact]
    public void HasLineOfSight_CacheNotCleared_ShouldUseCachedPath()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(10, 10, 
            new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var from = new HexCoordinates(2, 2);
        var to = new HexCoordinates(6, 2);
        
        // Initial LOS with no obstacles
        var initialLos = map.HasLineOfSight(from, to);
        initialLos.ShouldBeTrue("Should have LOS with no obstacles");

        // Add heavy forest but don't clear the cache
        map.GetHex(new HexCoordinates(3, 3))!.AddTerrain(new HeavyWoodsTerrain());
        map.GetHex(new HexCoordinates(4, 2))!.AddTerrain(new HeavyWoodsTerrain());

        // Act
        var losWithCache = map.HasLineOfSight(from, to);

        // Assert
        losWithCache.ShouldBeTrue("Should still have LOS when using cached path");
        losWithCache.ShouldBe(initialLos, "LOS result should not change without cache clear");
    }

    [Fact]
    public void ToData_ReturnsCorrectHexDataList()
    {
        // Arrange
        var map = new BattleMap(3, 3);
        
        // Add hexes with different terrains and levels
        var hex1 = new Hex(new HexCoordinates(1, 1));
        hex1.AddTerrain(new ClearTerrain());
        map.AddHex(hex1);
        
        var hex2 = new Hex(new HexCoordinates(2, 2), 1);
        hex2.AddTerrain(new LightWoodsTerrain());
        map.AddHex(hex2);
        
        var hex3 = new Hex(new HexCoordinates(3, 3), 2);
        hex3.AddTerrain(new HeavyWoodsTerrain());
        map.AddHex(hex3);
        
        var hex4 = new Hex(new HexCoordinates(3, 1), 2);
        hex4.AddTerrain(new ClearTerrain());
        hex4.AddTerrain(new LightWoodsTerrain());
        hex4.AddTerrain(new HeavyWoodsTerrain());
        map.AddHex(hex4);

        // Act
        var hexDataList = map.ToData();

        // Assert
        hexDataList.Count.ShouldBe(4);
        
        // Verify first hex data
        var hexData1 = hexDataList.First(h => h.Coordinates is { Q: 1, R: 1 });
        hexData1.Level.ShouldBe(0);
        hexData1.TerrainTypes.ShouldContain(MakaMekTerrains.Clear);
        
        // Verify second hex data
        var hexData2 = hexDataList.First(h => h.Coordinates is { Q: 2, R: 2 });
        hexData2.Level.ShouldBe(1);
        hexData2.TerrainTypes.ShouldContain(MakaMekTerrains.LightWoods);
        
        // Verify third hex data
        var hexData3 = hexDataList.First(h => h.Coordinates is { Q: 3, R: 3 });
        hexData3.Level.ShouldBe(2);
        hexData3.TerrainTypes.ShouldContain(MakaMekTerrains.HeavyWoods);
        
        // Verify third hex data
        var hexData4 = hexDataList.First(h => h.Coordinates is { Q: 3, R: 1 });
        hexData4.Level.ShouldBe(2);
        hexData4.TerrainTypes.ShouldContain(MakaMekTerrains.Clear);
        hexData4.TerrainTypes.ShouldContain(MakaMekTerrains.LightWoods);
        hexData4.TerrainTypes.ShouldContain(MakaMekTerrains.HeavyWoods);
    }
    
    [Fact]
    public void FindPath_ShouldCacheResult()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(3, 3), HexDirection.Bottom);

        // Act 1
        var path1 = map.FindPath(start, target, MovementType.Walk, 10);
        path1.ShouldNotBeNull();
        
        // Act 2
        var path2 = map.FindPath(start, target, MovementType.Walk, 10);
        
        // Assert
        path2.ShouldBeSameAs(path1);
    }

    [Fact]
    public void FindPath_WithProhibitedHexes_ShouldNotCache()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(3, 3), HexDirection.Bottom);
        var prohibited = new HashSet<HexCoordinates> { new(5, 5) }; // Prohibited hex (irrelevant to a path but triggers cache bypass)
        
        // Act 1
        var path1 = map.FindPath(start, target, MovementType.Walk, 10, prohibited);
        path1.ShouldNotBeNull();
        
        // Act 2
        var path2 = map.FindPath(start, target, MovementType.Walk, 10, prohibited);
        
        // Assert
        path2.ShouldNotBeSameAs(path1);
    }
    
    [Fact]
    public void FindJumpPath_ShouldCacheResult()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(1, 3), HexDirection.Bottom);

        // Act 1
        var path1 = map.FindPath(start, target, MovementType.Jump, 5);
        path1.ShouldNotBeNull();

        // Act 2
        var path2 = map.FindPath(start, target, MovementType.Jump, 5);

        // Assert
        path2.ShouldBeSameAs(path1);
    }

    [Fact]
    public void FindPath_LongestMode_MaximizesHexesTraveled()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(3, 1), HexDirection.Top);

        // Act - Find paths with both modes
        var shortestPath = map.FindPath(start, target, MovementType.Walk, 10);
        var longestPath = map.FindPath(start, target, MovementType.Walk, 10, null, PathfindingMode.Longest);

        // Assert
        shortestPath.ShouldNotBeNull();
        longestPath.ShouldNotBeNull();
        longestPath.HexesTraveled.ShouldBeGreaterThanOrEqualTo(shortestPath.HexesTraveled);
        longestPath.TotalCost.ShouldBeLessThanOrEqualTo(10);
    }

    [Fact]
    public void FindPath_LongestMode_RespectsMovementPointBudget()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(2, 1), HexDirection.Top);
        const int maxMovementPoints = 5;

        // Act
        var path = map.FindPath(start, target, MovementType.Walk, maxMovementPoints, null, PathfindingMode.Longest);

        // Assert
        path.ShouldNotBeNull();
        path.TotalCost.ShouldBeLessThanOrEqualTo(maxMovementPoints);
    }

    [Fact]
    public void FindPath_LongestMode_SameHex_ReturnsTurningSegments()
    {
        // Arrange
        var map = new BattleMap(2, 2);
        var hex = new Hex(new HexCoordinates(1, 1));
        map.AddHex(hex);
        var start = new HexPosition(hex.Coordinates, HexDirection.Top);
        var target = new HexPosition(hex.Coordinates, HexDirection.Bottom);

        // Act
        var path = map.FindPath(start, target, MovementType.Walk, 3, null, PathfindingMode.Longest);

        // Assert
        path.ShouldNotBeNull();
        path.Segments.Count.ShouldBe(3);
        path.HexesTraveled.ShouldBe(0);
        path.TotalCost.ShouldBe(3);
    }

    [Fact]
    public void FindPath_LongestMode_WithProhibitedHexes_AvoidsProhibitedAreas()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(3, 1), HexDirection.Top);
        var prohibited = new HashSet<HexCoordinates> { new(2, 1) };

        // Act
        var path = map.FindPath(start, target, MovementType.Walk, 10, prohibited, PathfindingMode.Longest);

        // Assert
        path.ShouldNotBeNull();
        path.Hexes.ShouldNotContain(prohibited.First());
    }

    [Fact]
    public void FindPath_LongestMode_WithMixedTerrain_ConsidersTerrainCosts()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(4, 2, new SingleTerrainGenerator(4, 2, new ClearTerrain()));

        // Add heavy woods hex that costs more
        var heavyWoodsHex = map.GetHex(new HexCoordinates(2, 1))!;
        heavyWoodsHex.RemoveTerrain(MakaMekTerrains.Clear);
        heavyWoodsHex.AddTerrain(new HeavyWoodsTerrain());

        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.TopRight);
        var target = new HexPosition(new HexCoordinates(3, 1), HexDirection.TopRight);

        // Act
        var path = map.FindPath(start, target, MovementType.Walk, 10, null, PathfindingMode.Longest);

        // Assert
        path.ShouldNotBeNull();
        path.TotalCost.ShouldBeLessThanOrEqualTo(10);
    }

    [Fact]
    public void FindPath_LongestMode_ReturnsNull_WhenTargetUnreachable()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(5, 5), HexDirection.Top);
        const int insufficientMovementPoints = 2;

        // Act
        var path = map.FindPath(start, target, MovementType.Walk, insufficientMovementPoints, null, PathfindingMode.Longest);

        // Assert
        path.ShouldBeNull();
    }

    [Fact]
    public void FindPath_LongestMode_WithTightBudget_FindsOptimalPath()
    {
        // Arrange
        var map = BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.TopRight);
        var target = new HexPosition(new HexCoordinates(2, 1), HexDirection.TopRight);
        const int tightBudget = 3;

        // Act
        var path = map.FindPath(start, target, MovementType.Walk, tightBudget, null, PathfindingMode.Longest);

        // Assert
        path.ShouldNotBeNull();
        path.TotalCost.ShouldBeLessThanOrEqualTo(tightBudget);
        path.HexesTraveled.ShouldBeGreaterThan(0);
    }
    
    [Fact]
    public void ConvertPathToSegments_WithInvalidHex_ThrowsWrongHexException()
    {
        // Arrange
        var map = new BattleMap(3, 3);
            
        // Add hexes for 3x3 map
        for (var q = 1; q <= 3; q++)
        {
            for (var r = 1; r <= 3; r++)
            {
                var hex = new Hex(new HexCoordinates(q, r));
                hex.AddTerrain(new ClearTerrain());
                map.AddHex(hex);
            }
        }
            
        // Create a path that includes an invalid coordinate outside the map
        var pathWithInvalidHex = new List<HexPosition>
        {
            new(new HexCoordinates(1, 3), HexDirection.Top),     // Valid hex on map
            new(new HexCoordinates(1, 5), HexDirection.Top)      // Invalid hex outside map
        };

        // Act & Assert
        var ex = Should.Throw<TargetInvocationException>(() => 
        {
            // Use reflection to call the private ConvertPathToSegments method
            var method = typeof(BattleMap).GetMethod("ConvertPathToSegments", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            method!.Invoke(map, [pathWithInvalidHex]);
        });

        // The inner exception should be WrongHexException
        ex.InnerException.ShouldBeOfType<WrongHexException>();
        var wrongHexEx = (WrongHexException)ex.InnerException!;
        wrongHexEx.Coordinates.ShouldBe(new HexCoordinates(1, 5));
        wrongHexEx.Message.ShouldContain("Hex not found");
    }
}
