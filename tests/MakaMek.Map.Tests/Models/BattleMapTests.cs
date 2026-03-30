using System.Reflection;
using Sanet.MakaMek.Map.Exceptions;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

public class BattleMapTests
{
    private static readonly IBattleMapFactory BattleMapFactory = new BattleMapFactory();
    [Fact]
    public void Constructor_SetsWidthAndHeight()
    {
        // Arrange & Act
        const int width = 5;
        const int height = 4;
        var sut = new BattleMap(width, height);

        // Assert
        sut.Width.ShouldBe(width);
        sut.Height.ShouldBe(height);
    }

    [Fact]
    public void AddHex_StoresHexInMap()
    {
        // Arrange
        var sut = new BattleMap(1, 1);
        var hex = new Hex(new HexCoordinates(1, 1));

        // Act
        sut.AddHex(hex);

        // Assert
        sut.GetHex(hex.Coordinates).ShouldBe(hex);
    }

    [Theory]
    [InlineData(-1, 0)]  // Left of the sut
    [InlineData(2, 0)]   // Right of the sut
    [InlineData(0, -1)]  // Above the sut
    [InlineData(0, 2)]   // Below the sut
    public void AddHex_OutsideMapBoundaries_ThrowsException(int q, int r)
    {
        // Arrange
        var sut = new BattleMap(2, 2);
        var hex = new Hex(new HexCoordinates(q, r));

        // Act & Assert
        var ex =Should.Throw<HexOutsideOfMapBoundariesException>(()=>sut.AddHex(hex));
        ex.Coordinates.ShouldBe(hex.Coordinates);
        ex.MapWidth.ShouldBe(2); 
        ex.MapHeight.ShouldBe(2);
    }

    [Fact]
    public void FindPath_WithFacingChanges_ConsidersTurningCost()
    {
        // Arrange
        var sut = new BattleMap(3, 1);
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(3, 1), HexDirection.Bottom);

        // Add hexes with clear terrain
        for (var q = 1; q <= 3; q++)
        {
            var hex = new Hex(new HexCoordinates(q, 1));
            hex.AddTerrain(new ClearTerrain());
            sut.AddHex(hex);
        }

        // Act
        var path = sut.FindPath(start, target, MovementType.Walk, 10);

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
        var sut = BattleMapFactory.GenerateMap(5, 5,
            new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(3, 3), HexDirection.Top);

        // Act
        var reachable = sut.GetReachableHexes(start, 2).ToList();

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
        var sut = BattleMapFactory.GenerateMap(2, 2,
            new SingleTerrainGenerator(2, 2, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);

        // Add clear terrain hex
        var clearHex = sut.GetHex(new HexCoordinates(2, 1))!;

        // Add heavy woods hex
        var woodsHex = new Hex(new HexCoordinates(1, 2));
        woodsHex.AddTerrain(new HeavyWoodsTerrain());
        sut.AddHex(woodsHex);

        // Act
        var reachable = sut.GetReachableHexes(start, 2).ToList();

        // Assert
        reachable.Count.ShouldBe(2); // Only the clear hex should be reachable
        reachable.Last().coordinates.ShouldBe(clearHex.Coordinates);
    }

    [Fact]
    public void GetLineOfSight_WithClearPath_HasLineOfSightTrue()
    {
        // Arrange
        var sut = new BattleMap(4, 1);
        var start = new HexCoordinates(1, 1);
        var end = new HexCoordinates(4, 1);

        // Add clear terrain hexes
        for (var q = 1; q <= 4; q++)
        {
            var hex = new Hex(new HexCoordinates(q, 1));
            hex.AddTerrain(new ClearTerrain());
            sut.AddHex(hex);
        }

        // Act
        var result = sut.GetLineOfSight(start, end, 2);

        // Assert
        result.HasLineOfSight.ShouldBeTrue();
    }
    
    [Fact]
    public void GetLineOfSight_WithMissingFromHex_ReturnsBlockedLos()
    {
        // Arrange
        var sut = new BattleMap(4, 1);
        var start = new HexCoordinates(1, 1);
        var end = new HexCoordinates(4, 1);
        sut.AddHex(new Hex(end));

        // Act
        var result = sut.GetLineOfSight(start, end, 2);

        // Assert
        result.HasLineOfSight.ShouldBeFalse();
        result.BlockReason.ShouldBe(LineOfSightBlockReason.InvalidCoordinates);
        result.BlockingHexCoordinates.ShouldBe(start);
    }
    
    [Fact]
    public void GetLineOfSight_WithMissingToHex_ReturnsBlockedLos()
    {
        // Arrange
        var sut = new BattleMap(4, 1);
        var start = new HexCoordinates(1, 1);
        var end = new HexCoordinates(4, 1);
        sut.AddHex(new Hex(start));

        // Act
        var result = sut.GetLineOfSight(start, end, 2);

        // Assert
        result.HasLineOfSight.ShouldBeFalse();
        result.BlockReason.ShouldBe(LineOfSightBlockReason.InvalidCoordinates);
        result.BlockingHexCoordinates.ShouldBe(end);
    }
    
    [Fact]
    public void GetLineOfSight_WithMissingHexInBetween_ReturnsBlockedLos()
    {
        // Arrange
        var sut = new BattleMap(1, 4);
        var start = new HexCoordinates(1, 1);
        var end = new HexCoordinates(1, 4);
        sut.AddHex(new Hex(start));
        sut.AddHex(new Hex(end));

        // Act
        var result = sut.GetLineOfSight(start, end, 2);

        // Assert
        result.HasLineOfSight.ShouldBeFalse();
        result.BlockReason.ShouldBe(LineOfSightBlockReason.InvalidCoordinates);
        result.BlockingHexCoordinates.ShouldBe(new HexCoordinates(1, 2));
    }

    [Fact]
    public void GetLineOfSight_WithBlockingTerrain_HasLineOfSightFalse()
    {
        // Arrange
        var sut = new BattleMap(4, 1);
        var start = new HexCoordinates(1, 1);
        var end = new HexCoordinates(4, 1);

        // Add clear terrain hexes
        for (var q = 1; q <= 4; q++)
        {
            var hex = new Hex(new HexCoordinates(q, 1));
            hex.AddTerrain(new ClearTerrain());
            sut.AddHex(hex);
        }

        // Add blocking heavy woods in the middle
        var blockingHex = new Hex(new HexCoordinates(2, 1), 2); // Higher base level
        blockingHex.AddTerrain(new HeavyWoodsTerrain()); // Adds 2 more levels
        sut.AddHex(blockingHex);

        // Act
        var result = sut.GetLineOfSight(start, end, 2);

        // Assert
        result.HasLineOfSight.ShouldBeFalse();
    }

    [Fact]
    public void GetReachableHexes_WithComplexTerrainAndFacing_ReachesHexThroughClearPath()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(11, 9,
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
            var hex = sut.GetHex(new HexCoordinates(q, r));
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
            var hex = sut.GetHex(new HexCoordinates(q, r));
            hex!.RemoveTerrain(MakaMekTerrains.Clear);
            hex.AddTerrain(new LightWoodsTerrain());
        }

        // Starting position: 9,5 facing bottom-left (direction 4)
        var start = new HexPosition(new HexCoordinates(9, 5), HexDirection.BottomLeft);
        const int maxMp = 5;

        // Act
        var reachableHexes = sut.GetReachableHexes(start, maxMp).ToList();

        // Assert
        var targetHex = new HexCoordinates(7, 8);
        reachableHexes.ShouldContain(x => x.coordinates == targetHex,
            "Hex (7,8) should be reachable through path: (9,5)->(8,5)->(7,6)->[turn]->(7,7)->(7,8)");

        // Verify the path exists and respects movement points
        var path = sut.FindPath(
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
        var sut = BattleMapFactory.GenerateMap(3, 3,
            new SingleTerrainGenerator(3,3, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(2, 2), HexDirection.Top);

        // Create prohibited hexes - block two adjacent hexes
        var prohibitedHexes = new HashSet<HexCoordinates>
        {
            new(2, 1), // Hex above start
            new(3, 2)  // Hex to the right of start
        };

        // Act
        var reachable = sut.GetReachableHexes(start, 2, prohibitedHexes).ToList();

        // Assert
        reachable.ShouldNotBeEmpty("Some hexes should be reachable");
        reachable.ShouldNotContain(h => prohibitedHexes.Contains(h.coordinates), 
            "Prohibited hexes should not be included in reachable hexes");
    }

    [Fact]
    public void FindPath_WithProhibitedHexes_FindsAlternativePath()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(3, 3,
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
        var path = sut.FindPath(start, target, MovementType.Walk, 10, prohibitedHexes);

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
        var sut = BattleMapFactory.GenerateMap(2, 5, 
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
            sut.AddHex(hex);
        }

        // Act
        var path = sut.FindPath(start, target, MovementType.Walk, 9);

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
        var sut = BattleMapFactory.GenerateMap(5, 5,
            new SingleTerrainGenerator(5, 5, terrain));
        var start = new HexCoordinates(3, 3);
        const int movementPoints = 2; 

        // Act
        var reachableHexes = sut.GetJumpReachableHexes(start, movementPoints).ToList();

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
        var sut = BattleMapFactory.GenerateMap(5, 5,
            new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var start = new HexCoordinates(3, 3);
        const int movementPoints = 2;

        // Prohibit some adjacent hexes
        var prohibitedHexes = start.GetAllNeighbours().Take(3).ToHashSet();

        // Act
        var reachableHexes = sut.GetJumpReachableHexes(start, movementPoints, prohibitedHexes).ToList();

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
        var sut = BattleMapFactory.GenerateMap(3, 3,
            new SingleTerrainGenerator(3, 3, new ClearTerrain()));
        var start = new HexCoordinates(1, 1); // Corner hex
        const int movementPoints = 2;

        // Act
        var reachableHexes = sut.GetJumpReachableHexes(start, movementPoints).ToList();

        // Assert
        reachableHexes.ShouldAllBe(h => 
                h.Q >= 1 && h.Q <= 3 && 
                h.R >= 1 && h.R <= 3, 
            "All hexes should be within sut boundaries (Q: 1-3, R: 1-3)");

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
        var sut = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new HeavyWoodsTerrain()));
        var from = new HexPosition(new HexCoordinates(fromQ, fromR), HexDirection.Top);
        var to = new HexPosition(new HexCoordinates(toQ, toR), HexDirection.Bottom);

        // Act
        var path = sut.FindPath(from, to, MovementType.Jump, mp);

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
        var sut = BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new HeavyWoodsTerrain()));
        var from = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var to = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);

        // Act
        var path = sut.FindPath(from, to, MovementType.Jump, 1);

        // Assert
        path.ShouldNotBeNull("Path should be found regardless of terrain");
        path.Segments.Count.ShouldBe(1, "Path should have one segment for adjacent hex");
        path.Segments[0].Cost.ShouldBe(1, "Cost should be 1 regardless of terrain");
    }

    [Fact]
    public void FindJumpPath_ReturnsNullForInvalidPositions()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain()));
        var from = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var invalidTo = new HexPosition(new HexCoordinates(3, 3), HexDirection.Bottom);

        // Act
        var path = sut.FindPath(from, invalidTo, MovementType.Jump, 5);

        // Assert
        path.ShouldBeNull("Path should be null for invalid target position");
    }
    
    [Fact]
    public void IsOnMap_ReturnsTrueForValidCoordinates()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(5, 5,
            new SingleTerrainGenerator(5, 5, new ClearTerrain()));

        // Act
        var isOnMap = sut.IsOnMap(new HexCoordinates(3, 3));

        // Assert
        isOnMap.ShouldBeTrue();
    }
    
    [Fact]
    public void IsOnMap_ReturnsFalseForInvalidCoordinates()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(5, 5,
            new SingleTerrainGenerator(5, 5, new ClearTerrain()));

        // Act
        var isOnMap = sut.IsOnMap(new HexCoordinates(6, 6));

        // Assert
        isOnMap.ShouldBeFalse();
    }

    [Fact]
    public void FindPath_SameHex_ReturnsOnlyTurningSegments()
    {
        // Arrange
        var sut = new BattleMap(2, 2);
        var hex = new Hex(new HexCoordinates(1, 1));
        sut.AddHex(hex);
        var start = new HexPosition(hex.Coordinates, HexDirection.Top);
        var target = new HexPosition(hex.Coordinates, HexDirection.Bottom);

        // Act
        var path = sut.FindPath(start, target, MovementType.Walk, 3);

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
        var sut = new BattleMap(2, 2);
        var hex = new Hex(new HexCoordinates(1, 1));
        sut.AddHex(hex);
        var start = new HexPosition(hex.Coordinates, HexDirection.Top);
        var target = new HexPosition(hex.Coordinates, HexDirection.Bottom);

        // Act
        var path = sut.FindPath(start, target, MovementType.Walk, 2); // Only 2 movement points for 3 turns

        // Assert
        path.ShouldBeNull();
    }

    [Fact]
    public void FindPath_SameHex_NoTurningNeeded_ReturnsOneSegment_WithNoHexes_AndNoTurns()
    {
        // Arrange
        var sut = new BattleMap(2, 2);
        var hex = new Hex(new HexCoordinates(1, 1));
        sut.AddHex(hex);
        var position = new HexPosition(hex.Coordinates, HexDirection.Top);

        // Act
        var path = sut.FindPath(position, position, MovementType.Jump, 3);

        // Assert
        path.ShouldNotBeNull();
        path.Segments.Count.ShouldBe(1);
        path.Hexes.Count.ShouldBe(1);
        path.HexesTraveled.ShouldBe(0);
        path.TurnsTaken.ShouldBe(0);
        path.TotalCost.ShouldBe(0);
    }

    [Fact]
    public void GetLineOfSight_HasLineOfSightFalse_WhenCoordinatesAreInvalid()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var invalidCoord = new HexCoordinates(0, 0);
        var validCoord = new HexCoordinates(1, 1);

        // Act & Assert
        sut.GetLineOfSight(invalidCoord, validCoord, 2).HasLineOfSight.ShouldBeFalse();
        sut.GetLineOfSight(validCoord, invalidCoord, 2).HasLineOfSight.ShouldBeFalse();
    }

    [Fact]
    public void GetLineOfSight_HasLineOfSightTrue_WhenSameHex()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var coord = new HexCoordinates(1, 1);

        // Act & Assert
        sut.GetLineOfSight(coord, coord, 2).HasLineOfSight.ShouldBeTrue();
    }

    [Fact]
    public void GetLineOfSight_HasLineOfSightTrue_WhenNoInterveningTerrain()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(1, 3);

        // Act & Assert
        sut.GetLineOfSight(from, to, 2).HasLineOfSight.ShouldBeTrue();
    }

    [Fact]
    public void GetLineOfSight_HasLineOfSightTrue_WhenAdjacentHexes()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(1, 2);

        // Act & Assert
        sut.GetLineOfSight(from, to, 2).HasLineOfSight.ShouldBeTrue();
    }

    [Fact]
    public void GetLineOfSight_HasLineOfSightTrue_WhenInterveningFactorLessThanThree()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(1, 4);

        // Set two light woods (factor 1 each) in between
        var hex1 = sut.GetHex(new HexCoordinates(1, 2))!;
        hex1.RemoveTerrain(MakaMekTerrains.Clear);
        hex1.AddTerrain(new LightWoodsTerrain());

        var hex2 = sut.GetHex(new HexCoordinates(1, 3))!;
        hex2.RemoveTerrain(MakaMekTerrains.Clear);
        hex2.AddTerrain(new LightWoodsTerrain());

        // Act & Assert
        sut.GetLineOfSight(from, to, 2).HasLineOfSight.ShouldBeTrue();
    }

    [Fact]
    public void GetLineOfSight_HasLineOfSightFalse_WhenInterveningFactorEqualsThree()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(1, 4);

        // Set terrain with a total factor of 3 (HeavyWoods=2, LightWoods=1)
        var hex1 = sut.GetHex(new HexCoordinates(1, 2))!;
        hex1.RemoveTerrain(MakaMekTerrains.Clear);
        hex1.AddTerrain(new HeavyWoodsTerrain());

        var hex2 = sut.GetHex(new HexCoordinates(1, 3))!;
        hex2.RemoveTerrain(MakaMekTerrains.Clear);
        hex2.AddTerrain(new LightWoodsTerrain());

        // Act & Assert
        sut.GetLineOfSight(from, to, 2).HasLineOfSight.ShouldBeFalse();
    }

    [Fact]
    public void GetLineOfSight_HasLineOfSightFalse_WhenInterveningFactorGreaterThanThree()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(1, 4);

        // Set terrain with a total factor of 4 (HeavyWoods=2 each)
        var hex1 = sut.GetHex(new HexCoordinates(1, 2))!;
        hex1.RemoveTerrain(MakaMekTerrains.Clear);
        hex1.AddTerrain(new HeavyWoodsTerrain());

        var hex2 = sut.GetHex(new HexCoordinates(1, 3))!;
        hex2.RemoveTerrain(MakaMekTerrains.Clear);
        hex2.AddTerrain(new HeavyWoodsTerrain());

        // Act & Assert
        sut.GetLineOfSight(from, to, 2).HasLineOfSight.ShouldBeFalse();
    }

    [Fact]
    public void GetLineOfSight_IgnoresStartAndEndHexTerrain()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(1, 3);

        // Set HeavyWoods (factor 2) at start and end - should be ignored
        var fromHex = sut.GetHex(from)!;
        fromHex.RemoveTerrain(MakaMekTerrains.Clear);
        fromHex.AddTerrain(new HeavyWoodsTerrain());

        var toHex = sut.GetHex(to)!;
        toHex.RemoveTerrain(MakaMekTerrains.Clear);
        toHex.AddTerrain(new HeavyWoodsTerrain());

        // Set LightWoods (factor 1) in between - not enough to block LOS
        var middleHex = sut.GetHex(new HexCoordinates(1, 2))!;
        middleHex.RemoveTerrain(MakaMekTerrains.Clear);
        middleHex.AddTerrain(new LightWoodsTerrain());

        // Act & Assert
        sut.GetLineOfSight(from, to, 2).HasLineOfSight.ShouldBeTrue();
    }

    [Fact]
    public void GetLineOfSight_WithHeavyWoodsCluster_ShouldBlockLOS()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
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
            var hex = sut.GetHex(coord)!;
            hex.RemoveTerrain(MakaMekTerrains.Clear);
            hex.AddTerrain(new HeavyWoodsTerrain());
        }

        // Act
        var result = sut.GetLineOfSight(attacker, target, 2);

        // Assert
        // LOS should be blocked because the line passes through multiple heavy woods hexes
        // Each heavy wood has an intervening factor of 2, and total intervening factor >= 3 blocks LOS
        result.HasLineOfSight.ShouldBeFalse($"LOS should be blocked by heavy woods cluster between {attacker} and {target}");
    }

    [Fact]
    public void GetLineOfSight_DividedLine_ShouldPreferDefenderOption()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var start = new HexCoordinates(2, 2);
        var end = new HexCoordinates(6, 2);

        // Add heavy forest to (3,2) and (4,2)
        sut.GetHex(new HexCoordinates(3, 2))!.AddTerrain(new HeavyWoodsTerrain());
        sut.GetHex(new HexCoordinates(4, 2))!.AddTerrain(new HeavyWoodsTerrain());

        // Act & Assert
        sut.GetLineOfSight(start, end, 2).HasLineOfSight.ShouldBeFalse(
            "LOS should be blocked because the defender's path through (3,2) with heavy forest is preferred");
    }

    [Fact]
    public void GetLineOfSight_DividedLine_ShouldPreferDefenderSecondaryOption()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(10, 10,
            new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var start = new HexCoordinates(2, 2);
        var end = new HexCoordinates(6, 2);

        // Add heavy forest to (3,2) and (4,2)
        sut.GetHex(new HexCoordinates(3, 3))!.AddTerrain(new HeavyWoodsTerrain());
        sut.GetHex(new HexCoordinates(4, 2))!.AddTerrain(new HeavyWoodsTerrain());

        // Act & Assert
        sut.GetLineOfSight(start, end, 2).HasLineOfSight.ShouldBeFalse(
            "LOS should be blocked because the defender's path through (3,2) with heavy forest is preferred");
    }

    [Fact]
    public void GetLineOfSight_CacheCleared_ShouldRecalculatePath()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(10, 10,
            new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var from = new HexCoordinates(2, 2);
        var to = new HexCoordinates(6, 2);

        // Initial LOS with no obstacles
        var initialResult = sut.GetLineOfSight(from, to, 2);
        initialResult.HasLineOfSight.ShouldBeTrue("Should have LOS with no obstacles");

        // Add heavy forest to block LOS
        sut.GetHex(new HexCoordinates(3, 3))!.AddTerrain(new HeavyWoodsTerrain());
        sut.GetHex(new HexCoordinates(4, 2))!.AddTerrain(new HeavyWoodsTerrain());

        // Clear cache to force recalculation
        sut.ClearLosCache();

        // Act & Assert
        var resultAfterChange = sut.GetLineOfSight(from, to, 2);
        resultAfterChange.HasLineOfSight.ShouldBeFalse("LOS should be blocked after adding forest and clearing cache");
    }

    [Fact]
    public void GetLineOfSight_CacheNotCleared_ShouldUseCachedPath()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(10, 10,
            new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        var from = new HexCoordinates(2, 2);
        var to = new HexCoordinates(6, 2);

        // Initial LOS with no obstacles
        var initialResult = sut.GetLineOfSight(from, to, 2);
        initialResult.HasLineOfSight.ShouldBeTrue("Should have LOS with no obstacles");

        // Add heavy forest but don't clear the cache
        sut.GetHex(new HexCoordinates(3, 3))!.AddTerrain(new HeavyWoodsTerrain());
        sut.GetHex(new HexCoordinates(4, 2))!.AddTerrain(new HeavyWoodsTerrain());

        // Act
        var cachedResult = sut.GetLineOfSight(from, to, 2);

        // Assert
        cachedResult.HasLineOfSight.ShouldBeTrue("Should still have LOS when using cached path");
        cachedResult.HasLineOfSight.ShouldBe(initialResult.HasLineOfSight, "LOS result should not change without cache clear");
    }
    
    [Fact]
    public void GetLineOfSight_WithLosOverWoods_ShouldNotBlock()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(1, 5,
            new SingleTerrainGenerator(1, 5, new ClearTerrain()));
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(1, 5);

        sut.AddHex(new Hex(from,2));
        sut.AddHex(new Hex(to,2));

        sut.GetHex(new HexCoordinates(1, 2))!.AddTerrain(new HeavyWoodsTerrain());
        sut.GetHex(new HexCoordinates(1, 3))!.AddTerrain(new HeavyWoodsTerrain());
        sut.GetHex(new HexCoordinates(1, 4))!.AddTerrain(new HeavyWoodsTerrain());

        // Act
        var result = sut.GetLineOfSight(from, to, 2);

        // Assert
        result.HasLineOfSight.ShouldBeTrue("Should have LOS with no obstacles");
    }

    [Fact]
    public void GetLineOfSight_WithAttackerHeight2_CanSeeOverLevel1Terrain()
    {
        // Arrange
        var sut = new BattleMap(1, 5);
        var start = new HexCoordinates(1, 1);
        var end = new HexCoordinates(1, 5);

        // Add hexes - attacker at level 0, blocking terrain at level 1, target at level 0
        sut.AddHex(new Hex(start)); // Attacker hex at level 0
        sut.AddHex(new Hex(new HexCoordinates(1, 2)));
        sut.AddHex(new Hex(new HexCoordinates(1, 3), 1)); // Level 1 terrain in between
        sut.AddHex(new Hex(new HexCoordinates(1, 4)));
        sut.AddHex(new Hex(end)); // Target hex at level 0

        // Act & Assert
        // Without unit height, LOS is blocked by level 1 terrain (interpolated height at midpoint is 0)
        sut.GetLineOfSight(start, end, 0).HasLineOfSight.ShouldBeFalse("LOS blocked without unit height");
        // With standing mech height (2), can see over level 1 terrain (interpolated height at midpoint is 2)
        sut.GetLineOfSight(start, end, 2, 2).HasLineOfSight.ShouldBeTrue("LOS clear with mech height 2");
    }

    [Fact]
    public void GetLineOfSight_WithProneAttacker_BlockedByLevel1Terrain()
    {
        // Arrange
        var sut = new BattleMap(1, 5);
        var start = new HexCoordinates(1, 1);
        var end = new HexCoordinates(1, 5);

        // Add hexes - attacker at level 0, blocking terrain at level 1, target at level 0
        sut.AddHex(new Hex(start));
        sut.AddHex(new Hex(new HexCoordinates(1, 2)));
        sut.AddHex(new Hex(new HexCoordinates(1, 3), 1)); // Level 1 terrain in between
        sut.AddHex(new Hex(new HexCoordinates(1, 4)));
        sut.AddHex(new Hex(end));

        // Act & Assert
        // Standing mech (height 2) can see over level 1 terrain
        sut.GetLineOfSight(start, end, 2, 2).HasLineOfSight.ShouldBeTrue("LOS clear with standing mech");
        // Prone mech (height 1) cannot see over level 1 terrain
        sut.GetLineOfSight(start, end, 1, 1).HasLineOfSight.ShouldBeFalse("LOS blocked for prone mech");
    }

    [Fact]
    public void GetLineOfSight_WithTargetHeight_HasBetterLOS()
    {
        // Arrange
        var sut = new BattleMap(1, 5);
        var start = new HexCoordinates(1, 1);
        var end = new HexCoordinates(1, 5);

        // Add hexes - attacker at level 0, small hill at level 1, target at level 0
        sut.AddHex(new Hex(start));
        sut.AddHex(new Hex(new HexCoordinates(1, 2)));
        sut.AddHex(new Hex(new HexCoordinates(1, 3), 1)); // Level 1 terrain
        sut.AddHex(new Hex(new HexCoordinates(1, 4)));
        sut.AddHex(new Hex(end));

        // Act & Assert
        // Attacker height 0 (no unit), target height 0 - blocked
        sut.GetLineOfSight(start, end, 0).HasLineOfSight.ShouldBeFalse("LOS blocked with no heights");
        // Attacker height 2, target height 0 - clear (standing mech can see over level 1)
        sut.GetLineOfSight(start, end, 2,1).HasLineOfSight.ShouldBeTrue("LOS clear with attacker height 2");
    }

    [Fact]
    public void ToData_ReturnsCorrectHexDataList()
    {
        // Arrange
        var sut = new BattleMap(3, 3);
        
        // Add hexes with different terrains and levels
        var hex1 = new Hex(new HexCoordinates(1, 1));
        hex1.AddTerrain(new ClearTerrain());
        sut.AddHex(hex1);
        
        var hex2 = new Hex(new HexCoordinates(2, 2), 1);
        hex2.AddTerrain(new LightWoodsTerrain());
        sut.AddHex(hex2);
        
        var hex3 = new Hex(new HexCoordinates(3, 3), 2);
        hex3.AddTerrain(new HeavyWoodsTerrain());
        sut.AddHex(hex3);
        
        var hex4 = new Hex(new HexCoordinates(3, 1), 2);
        hex4.AddTerrain(new ClearTerrain());
        hex4.AddTerrain(new LightWoodsTerrain());
        hex4.AddTerrain(new HeavyWoodsTerrain());
        sut.AddHex(hex4);

        // Act
        var hexDataList = sut.ToData();

        // Assert
        hexDataList.Biome.ShouldBe(sut.Biome);
        hexDataList.HexData.Count.ShouldBe(4);

        // Verify first hex data
        var hexData1 = hexDataList.HexData.First(h => h.Coordinates is { Q: 1, R: 1 });
        hexData1.Level.ShouldBe(0);
        hexData1.TerrainTypes.ShouldContain(MakaMekTerrains.Clear);

        // Verify second hex data
        var hexData2 = hexDataList.HexData.First(h => h.Coordinates is { Q: 2, R: 2 });
        hexData2.Level.ShouldBe(1);
        hexData2.TerrainTypes.ShouldContain(MakaMekTerrains.LightWoods);

        // Verify third hex data
        var hexData3 = hexDataList.HexData.First(h => h.Coordinates is { Q: 3, R: 3 });
        hexData3.Level.ShouldBe(2);
        hexData3.TerrainTypes.ShouldContain(MakaMekTerrains.HeavyWoods);

        // Verify third hex data
        var hexData4 = hexDataList.HexData.First(h => h.Coordinates is { Q: 3, R: 1 });
        hexData4.Level.ShouldBe(2);
        hexData4.TerrainTypes.ShouldContain(MakaMekTerrains.Clear);
        hexData4.TerrainTypes.ShouldContain(MakaMekTerrains.LightWoods);
        hexData4.TerrainTypes.ShouldContain(MakaMekTerrains.HeavyWoods);
    }
    
    [Theory]
    [InlineData(PathFindingMode.Shortest)]
    [InlineData(PathFindingMode.Longest)]
    public void FindPath_ShouldCacheResult(PathFindingMode pathFindingMode)
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(3, 3), HexDirection.Bottom);

        // Act 1
        var path1 = sut.FindPath(start, target, MovementType.Walk, 10, null, pathFindingMode);
        path1.ShouldNotBeNull();
        
        // Act 2
        var path2 = sut.FindPath(start, target, MovementType.Walk, 10, null, pathFindingMode);
        
        // Assert
        path2.ShouldBeSameAs(path1);
    }

    [Fact]
    public void FindPath_ShortAndLongModes_UseDifferentCaches()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(3, 3), HexDirection.Bottom);

        // Act
        var shortestPath = sut.FindPath(start, target, MovementType.Walk, 10);
        var longestPath = sut.FindPath(start, target, MovementType.Walk, 10, null, PathFindingMode.Longest);
        
        // Assert
        shortestPath.ShouldNotBeNull();
        longestPath.ShouldNotBeNull();
        
        // The paths should be different instances since they use different caches
        shortestPath.ShouldNotBeSameAs(longestPath);
        
        // The longest path should have equal or more hexes traveled
        longestPath.HexesTraveled.ShouldBeGreaterThanOrEqualTo(shortestPath.HexesTraveled);
    }

    [Fact]
    public void FindPath_WithProhibitedHexes_ShouldNotCache()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(3, 3), HexDirection.Bottom);
        var prohibited = new HashSet<HexCoordinates> { new(5, 5) }; // Prohibited hex (irrelevant to a path but triggers cache bypass)
        
        // Act 1
        var path1 = sut.FindPath(start, target, MovementType.Walk, 10, prohibited);
        path1.ShouldNotBeNull();
        
        // Act 2
        var path2 = sut.FindPath(start, target, MovementType.Walk, 10, prohibited);
        
        // Assert
        path2.ShouldNotBeSameAs(path1);
    }
    
    [Fact]
    public void FindJumpPath_ShouldCacheResult()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(1, 3), HexDirection.Bottom);

        // Act 1
        var path1 = sut.FindPath(start, target, MovementType.Jump, 5);
        path1.ShouldNotBeNull();

        // Act 2
        var path2 = sut.FindPath(start, target, MovementType.Jump, 5);

        // Assert
        path2.ShouldBeSameAs(path1);
    }

    [Fact]
    public void FindPath_LongestMode_MaximizesHexesTraveled()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(3, 1), HexDirection.Top);

        // Act - Find paths with both modes
        var shortestPath = sut.FindPath(start, target, MovementType.Walk, 10);
        var longestPath = sut.FindPath(start, target, MovementType.Walk, 10, null, PathFindingMode.Longest);

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
        var sut = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(2, 1), HexDirection.Top);
        const int maxMovementPoints = 5;

        // Act
        var path = sut.FindPath(start, target, MovementType.Walk, maxMovementPoints, null, PathFindingMode.Longest);

        // Assert
        path.ShouldNotBeNull();
        path.TotalCost.ShouldBeLessThanOrEqualTo(maxMovementPoints);
    }

    [Fact]
    public void FindPath_LongestMode_SameHex_ReturnsTurningSegments()
    {
        // Arrange
        var sut = new BattleMap(2, 2);
        var hex = new Hex(new HexCoordinates(1, 1));
        sut.AddHex(hex);
        var start = new HexPosition(hex.Coordinates, HexDirection.Top);
        var target = new HexPosition(hex.Coordinates, HexDirection.Bottom);

        // Act
        var path = sut.FindPath(start, target, MovementType.Walk, 3, null, PathFindingMode.Longest);

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
        var sut = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(3, 1), HexDirection.Top);
        var prohibited = new HashSet<HexCoordinates> { new(2, 1) };

        // Act
        var path = sut.FindPath(start, target, MovementType.Walk, 10, prohibited, PathFindingMode.Longest);

        // Assert
        path.ShouldNotBeNull();
        path.Hexes.ShouldNotContain(prohibited.First());
    }

    [Fact]
    public void FindPath_LongestMode_WithMixedTerrain_ConsidersTerrainCosts()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(4, 2, new SingleTerrainGenerator(4, 2, new ClearTerrain()));

        // Add heavy woods hex that costs more
        var heavyWoodsHex = sut.GetHex(new HexCoordinates(2, 1))!;
        heavyWoodsHex.RemoveTerrain(MakaMekTerrains.Clear);
        heavyWoodsHex.AddTerrain(new HeavyWoodsTerrain());

        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.TopRight);
        var target = new HexPosition(new HexCoordinates(3, 1), HexDirection.TopRight);

        // Act
        var path = sut.FindPath(start, target, MovementType.Walk, 10, null, PathFindingMode.Longest);

        // Assert
        path.ShouldNotBeNull();
        path.TotalCost.ShouldBeLessThanOrEqualTo(10);
    }

    [Fact]
    public void FindPath_LongestMode_ReturnsNull_WhenTargetUnreachable()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(5, 5), HexDirection.Top);
        const int insufficientMovementPoints = 2;

        // Act
        var path = sut.FindPath(start, target, MovementType.Walk, insufficientMovementPoints, null, PathFindingMode.Longest);

        // Assert
        path.ShouldBeNull();
    }

    [Fact]
    public void FindPath_LongestMode_WithTightBudget_FindsOptimalPath()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new ClearTerrain()));
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.TopRight);
        var target = new HexPosition(new HexCoordinates(2, 1), HexDirection.TopRight);
        const int tightBudget = 3;

        // Act
        var path = sut.FindPath(start, target, MovementType.Walk, tightBudget, null, PathFindingMode.Longest);

        // Assert
        path.ShouldNotBeNull();
        path.TotalCost.ShouldBeLessThanOrEqualTo(tightBudget);
        path.HexesTraveled.ShouldBeGreaterThan(0);
    }
    
    [Fact]
    public void ConvertPathToSegments_WithInvalidHex_ThrowsWrongHexException()
    {
        // Arrange
        var sut = new BattleMap(3, 3);
            
        // Add hexes for 3x3 sut
        for (var q = 1; q <= 3; q++)
        {
            for (var r = 1; r <= 3; r++)
            {
                var hex = new Hex(new HexCoordinates(q, r));
                hex.AddTerrain(new ClearTerrain());
                sut.AddHex(hex);
            }
        }
            
        // Create a path that includes an invalid coordinate outside the sut
        var pathWithInvalidHex = new List<HexPosition>
        {
            new(new HexCoordinates(1, 3), HexDirection.Top),     // Valid hex on sut
            new(new HexCoordinates(1, 5), HexDirection.Top)      // Invalid hex outside sut
        };

        // Act & Assert
        var ex = Should.Throw<TargetInvocationException>(() => 
        {
            // Use reflection to call the private ConvertPathToSegments method
            var method = typeof(BattleMap).GetMethod("ConvertPathToSegments", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            method!.Invoke(sut, [pathWithInvalidHex]);
        });

        // The inner exception should be WrongHexException
        ex.InnerException.ShouldBeOfType<WrongHexException>();
        var wrongHexEx = (WrongHexException)ex.InnerException!;
        wrongHexEx.Coordinates.ShouldBe(new HexCoordinates(1, 5));
        wrongHexEx.Message.ShouldContain("Hex not found");
    }
    
    [Fact]
    public void FindPath_WithLevelChange_AddsLevelCostToMovement()
    {
        // Arrange - Create a sut with elevation changes
        var sut = new BattleMap(3, 2);
        
        // Hex (1,1) -> (2,1) -> (3,1) path
        // (1,1) Q=1 (odd): BottomRight neighbor is (2,1)
        // (2,1) Q=2 (even): TopRight neighbor is (3,1)
        var hex1 = new Hex(new HexCoordinates(1, 1), level: 0);
        hex1.AddTerrain(new ClearTerrain());
        sut.AddHex(hex1);
        
        var hex2 = new Hex(new HexCoordinates(2, 1), level: 1);
        hex2.AddTerrain(new ClearTerrain());
        sut.AddHex(hex2);
        
        var hex3 = new Hex(new HexCoordinates(3, 1), level: 2);
        hex3.AddTerrain(new ClearTerrain());
        sut.AddHex(hex3);

        // Start facing BottomRight so no turn needed to move to (2,1)
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.BottomRight);
        var target = new HexPosition(new HexCoordinates(3, 1), HexDirection.TopRight);

        // Act - Find a path with enough MP to cover terrain + level costs
        // Move 1: 1 terrain + 1 level (0->1) = 2 MP
        // Turn at (2,1): from BottomRight to TopRight = 2 turns = 2 MP
        // Move 2: 1 terrain + 1 level (1->2) = 2 MP
        // Total: 6 MP
        var path = sut.FindPath(start, target, MovementType.Walk, 10, maxLevelChange: 2);

        // Assert
        path.ShouldNotBeNull();
        // Verify level costs are included (total should be > terrain-only cost of 2)
        path.TotalCost.ShouldBeGreaterThan(2);
    }

    [Fact]
    public void FindPath_WithLevelChangeDescending_AddsSymmetricLevelCost()
    {
        // Arrange - Create a sut with elevation changes (descending)
        var sut = new BattleMap(3, 2);
        
        // Hex (1,1) -> (2,1) -> (3,1) path
        var hex1 = new Hex(new HexCoordinates(1, 1), level: 2);
        hex1.AddTerrain(new ClearTerrain());
        sut.AddHex(hex1);
        
        var hex2 = new Hex(new HexCoordinates(2, 1), level: 1);
        hex2.AddTerrain(new ClearTerrain());
        sut.AddHex(hex2);
        
        var hex3 = new Hex(new HexCoordinates(3, 1), level: 0);
        hex3.AddTerrain(new ClearTerrain());
        sut.AddHex(hex3);

        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.BottomRight);
        var target = new HexPosition(new HexCoordinates(3, 1), HexDirection.TopRight);

        // Act - Find a path descending (level 2 -> 1 -> 0)
        var path = sut.FindPath(start, target, MovementType.Walk, 10, maxLevelChange: 2);

        // Assert
        path.ShouldNotBeNull();
        // Verify level costs are included (total should be > terrain-only cost of 2)
        path.TotalCost.ShouldBeGreaterThan(2);
    }

    [Fact]
    public void FindPath_WithTwoLevelChange_CostsTwoExtraMP()
    {
        // Arrange - Create a sut with 2-level change in one step
        var sut = new BattleMap(2, 2);
        
        // (1,1) Q=1 (odd): BottomRight neighbor is (2,1)
        var hex1 = new Hex(new HexCoordinates(1, 1), level: 0);
        hex1.AddTerrain(new ClearTerrain());
        sut.AddHex(hex1);
        
        var hex2 = new Hex(new HexCoordinates(2, 1), level: 2);
        hex2.AddTerrain(new ClearTerrain());
        sut.AddHex(hex2);

        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.BottomRight);
        var target = new HexPosition(new HexCoordinates(2, 1), HexDirection.BottomRight);

        // Act - Move from level 0 to level 2 (2 level change)
        // Cost: 1 (terrain) + 2 (level change) = 3 MP
        var path = sut.FindPath(start, target, MovementType.Walk, 5, maxLevelChange: 2);

        // Assert
        path.ShouldNotBeNull();
        path.TotalCost.ShouldBe(3, "Path should cost 3 MP: 1 terrain + 2 level change");
    }

    [Theory]
    [InlineData(PathFindingMode.Shortest)]
    [InlineData(PathFindingMode.Longest)]
    public void FindPath_WithMaxLevelChangeExceeded_SkipsHex(PathFindingMode pathFindingMode)
    {
        // Arrange - Create a sut with 3-level change (exceeds max of 2)
        var sut = new BattleMap(3, 2);
        
        // (1,1) Q=1 (odd): BottomRight neighbor is (2,1)
        var hex1 = new Hex(new HexCoordinates(1, 1), level: 0);
        hex1.AddTerrain(new ClearTerrain());
        sut.AddHex(hex1);
        
        var hex2 = new Hex(new HexCoordinates(2, 1), level: 3); // 3 levels up - exceeds max
        hex2.AddTerrain(new ClearTerrain());
        sut.AddHex(hex2);
        
        var hex3 = new Hex(new HexCoordinates(3, 1), level: 0);
        hex3.AddTerrain(new ClearTerrain());
        sut.AddHex(hex3);

        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.BottomRight);
        var target = new HexPosition(new HexCoordinates(2, 1), HexDirection.BottomRight);

        // Act - Try to move to hex with a 3-level change
        var path = sut.FindPath(start,
            target,
            MovementType.Walk,
            10,
            maxLevelChange: 2,
            pathFindingMode: pathFindingMode);

        // Assert
        path.ShouldBeNull("Path should not exist when level change exceeds maximum");
    }

    [Fact]
    public void FindPath_WithMaxLevelChange_RoutesAroundSteepHex()
    {
        // Arrange - Create a sut where a direct path has a 3-level change
        var sut = new BattleMap(3, 3);
        
        // Build a connected sut
        for (var q = 1; q <= 3; q++)
        {
            for (var r = 1; r <= 3; r++)
            {
                var level = 0;
                // Make (2,2) a steep hex (level 3)
                if (q == 2 && r == 2) level = 3;
                var hex = new Hex(new HexCoordinates(q, r), level: level);
                hex.AddTerrain(new ClearTerrain());
                sut.AddHex(hex);
            }
        }

        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.BottomRight);
        var target = new HexPosition(new HexCoordinates(3, 2), HexDirection.BottomRight);

        // Act - Path should go around the steep hex
        var path = sut.FindPath(start, target, MovementType.Walk, 10, maxLevelChange: 2);

        // Assert
        path.ShouldNotBeNull("Path should exist going around steep hex");
        path.Hexes.ShouldNotContain(new HexCoordinates(2, 2), "Should not go through steep hex");
    }

    [Fact]
    public void GetReachableHexes_WithLevelChange_LimitsReachability()
    {
        // Arrange - Create a sut with elevation
        var sut = new BattleMap(3, 2);
        
        // (1,1) -> (2,1) -> (3,1) path
        var hex1 = new Hex(new HexCoordinates(1, 1), level: 0);
        hex1.AddTerrain(new ClearTerrain());
        sut.AddHex(hex1);
        
        var hex2 = new Hex(new HexCoordinates(2, 1), level: 1);
        hex2.AddTerrain(new ClearTerrain());
        sut.AddHex(hex2);
        
        var hex3 = new Hex(new HexCoordinates(3, 1), level: 2);
        hex3.AddTerrain(new ClearTerrain());
        sut.AddHex(hex3);

        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.BottomRight);

        // Act - With limited MP, elevated hexes may be unreachable
        // Hex 2: 1 terrain + 1 level = 2 MP
        // Hex 3: 1+1 + 1+1 = 4 MP (through hex 2)
        var reachable = sut.GetReachableHexes(start, 3, maxLevelChange: 2).ToList();

        // Assert - Start hex is included with cost 0, plus hex 2 (2 MP)
        // Hex 3 requires 4 MP which exceeds 3
        reachable.Count.ShouldBe(2, "Should reach start hex (0 MP) and hex 2 (2 MP)");
        reachable.ShouldContain(r => r.coordinates == new HexCoordinates(2, 1));
        reachable.ShouldNotContain(r => r.coordinates == new HexCoordinates(3, 1));
    }

    [Fact]
    public void GetReachableHexes_WithSufficientMP_ClimbsToElevatedHexes()
    {
        // Arrange - Create a sut with elevation
        var sut = new BattleMap(3, 2);
        
        // (1,1) -> (2,1) -> (3,1) path
        var hex1 = new Hex(new HexCoordinates(1, 1), level: 0);
        hex1.AddTerrain(new ClearTerrain());
        sut.AddHex(hex1);
        
        var hex2 = new Hex(new HexCoordinates(2, 1), level: 1);
        hex2.AddTerrain(new ClearTerrain());
        sut.AddHex(hex2);
        
        var hex3 = new Hex(new HexCoordinates(3, 1), level: 2);
        hex3.AddTerrain(new ClearTerrain());
        sut.AddHex(hex3);

        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.BottomRight);

        // Act - With sufficient MP, all hexes reachable
        var reachable = sut.GetReachableHexes(start, 5, maxLevelChange: 2).ToList();

        // Assert - Start hex is included (cost 0), plus hex 2 (2 MP) and hex 3 (4 MP)
        reachable.Count.ShouldBe(3);
        reachable.ShouldContain(r => r.coordinates == new HexCoordinates(1, 1));
        reachable.ShouldContain(r => r.coordinates == new HexCoordinates(2, 1));
        reachable.ShouldContain(r => r.coordinates == new HexCoordinates(3, 1));
    }

    [Fact]
    public void GetReachableHexes_WithMaxLevelChange_ExcludesSteepHexes()
    {
        // Arrange - Create a sut with steep elevation
        var sut = new BattleMap(2, 2);
        
        // (1,1) Q=1 (odd): BottomRight neighbor is (2,1)
        var hex1 = new Hex(new HexCoordinates(1, 1), level: 0);
        hex1.AddTerrain(new ClearTerrain());
        sut.AddHex(hex1);
        
        var hex2 = new Hex(new HexCoordinates(2, 1), level: 3); // 3-level change
        hex2.AddTerrain(new ClearTerrain());
        sut.AddHex(hex2);

        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.BottomRight);

        // Act - Max level change of 2 should exclude the 3-level hex
        var reachable = sut.GetReachableHexes(start, 10, maxLevelChange: 2).ToList();

        // Assert - Only start hex reachable (cost 0), steep hex excluded
        reachable.Count.ShouldBe(1, "Only start hex should be reachable");
        reachable[0].coordinates.ShouldBe(new HexCoordinates(1, 1));
        reachable[0].cost.ShouldBe(0);
    }

    [Fact]
    public void GetReachableHexes_WithZeroMaxLevelChange_OnlySameLevelHexes()
    {
        // Arrange - Create a sut with mixed elevations
        var sut = new BattleMap(3, 2);
        
        // (1,1) Q=1 (odd): BottomRight neighbor is (2,1), a Bottom neighbor is (1,2)
        var hex1 = new Hex(new HexCoordinates(1, 1), level: 0);
        hex1.AddTerrain(new ClearTerrain());
        sut.AddHex(hex1);
        
        var hex2 = new Hex(new HexCoordinates(2, 1), level: 1); // Different level
        hex2.AddTerrain(new ClearTerrain());
        sut.AddHex(hex2);
        
        var hex3 = new Hex(new HexCoordinates(1, 2), level: 0); // Same level (via a Bottom direction)
        hex3.AddTerrain(new ClearTerrain());
        sut.AddHex(hex3);

        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);

        // Act - With maxLevelChange 0, only same-level hexes reachable
        var reachable = sut.GetReachableHexes(start, 10, maxLevelChange: 0).ToList();

        // Assert - Start hex + same-level neighbor
        reachable.Count.ShouldBe(2);
        reachable.ShouldContain(r => r.coordinates == new HexCoordinates(1, 1));
        reachable.ShouldContain(r => r.coordinates == new HexCoordinates(1, 2));
        reachable.ShouldNotContain(r => r.coordinates == new HexCoordinates(2, 1));
    }

    [Fact]
    public void FindPath_WithNoMaxLevelChange_AllowsAnyLevelChange()
    {
        // Arrange - Create a sut with steep elevation
        var sut = new BattleMap(2, 2);
        
        // (1,1) Q=1 (odd): BottomRight neighbor is (2,1)
        var hex1 = new Hex(new HexCoordinates(1, 1), level: 0);
        hex1.AddTerrain(new ClearTerrain());
        sut.AddHex(hex1);
        
        var hex2 = new Hex(new HexCoordinates(2, 1), level: 5); // 5-level change
        hex2.AddTerrain(new ClearTerrain());
        sut.AddHex(hex2);

        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.BottomRight);
        var target = new HexPosition(new HexCoordinates(2, 1), HexDirection.BottomRight);

        // Act - No maxLevelChange specified (null default)
        var path = sut.FindPath(start, target, MovementType.Walk, 10);

        // Assert
        path.ShouldNotBeNull("Path should exist when no max level change restriction");
        path.TotalCost.ShouldBe(6, "Cost: 1 terrain + 5 level change");
    }
    
    [Fact]
    public void GetJumpReachableHexes_IgnoresLevelChanges()
    {
        // Arrange - Create a sut with varying elevations
        var sut = new BattleMap(3, 1);
        
        var hex1 = new Hex(new HexCoordinates(1, 1), level: 0);
        hex1.AddTerrain(new ClearTerrain());
        sut.AddHex(hex1);
        
        var hex2 = new Hex(new HexCoordinates(2, 1), level: 5); // High elevation
        hex2.AddTerrain(new ClearTerrain());
        sut.AddHex(hex2);
        
        var hex3 = new Hex(new HexCoordinates(3, 1), level: 3);
        hex3.AddTerrain(new ClearTerrain());
        sut.AddHex(hex3);

        var start = new HexCoordinates(1, 1);

        // Act - Jump ignores level changes
        var reachable = sut.GetJumpReachableHexes(start, 2).ToList();

        // Assert
        reachable.Count.ShouldBe(2, "Both hexes should be reachable by jump regardless of level");
        reachable.ShouldContain(new HexCoordinates(2, 1));
        reachable.ShouldContain(new HexCoordinates(3, 1));
    }

    [Fact]
    public void FindJumpPath_IgnoresLevelCosts()
    {
        // Arrange - Create a sut with elevation change
        var sut = new BattleMap(2, 1);
        
        var hex1 = new Hex(new HexCoordinates(1, 1), level: 0);
        hex1.AddTerrain(new ClearTerrain());
        sut.AddHex(hex1);
        
        var hex2 = new Hex(new HexCoordinates(2, 1), level: 5); // 5-level difference
        hex2.AddTerrain(new ClearTerrain());
        sut.AddHex(hex2);

        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(2, 1), HexDirection.Top);

        // Act - Jump should cost 1 MP regardless of level
        var path = sut.FindPath(start, target, MovementType.Jump, 1);

        // Assert
        path.ShouldNotBeNull();
        path.TotalCost.ShouldBe(1, "Jump should cost 1 MP per hex, ignoring level changes");
    }

    [Fact]
    public void FindJumpPath_CanTraverseAnyLevelChange()
    {
        // Arrange - Create a sut with extreme elevation change
        var sut = new BattleMap(2, 1);
        
        var hex1 = new Hex(new HexCoordinates(1, 1), level: 0);
        hex1.AddTerrain(new ClearTerrain());
        sut.AddHex(hex1);
        
        var hex2 = new Hex(new HexCoordinates(2, 1), level: 10); // 10-level difference
        hex2.AddTerrain(new ClearTerrain());
        sut.AddHex(hex2);

        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var target = new HexPosition(new HexCoordinates(2, 1), HexDirection.Top);

        // Act - Jump can traverse any level change
        var path = sut.FindPath(start, target, MovementType.Jump, 1);

        // Assert
        path.ShouldNotBeNull("Jump should be able to traverse any level change");
    }

    [Fact]
    public void GetLevelDifference_WithValidHexes_ReturnsCorrectDifference()
    {
        // Arrange
        var sut = new BattleMap(2, 2);
        var hex1 = new Hex(new HexCoordinates(1, 1), 5);
        var hex2 = new Hex(new HexCoordinates(2, 2), 3);
        sut.AddHex(hex1);
        sut.AddHex(hex2);

        // Act
        var difference = sut.GetLevelDifference(new HexCoordinates(1, 1), new HexCoordinates(2, 2));

        // Assert
        difference.ShouldBe(2); // 5 - 3 = 2
    }

    [Fact]
    public void GetLevelDifference_WithNegativeDifference_ReturnsCorrectValue()
    {
        // Arrange
        var sut = new BattleMap(2, 2);
        var hex1 = new Hex(new HexCoordinates(1, 1), 2);
        var hex2 = new Hex(new HexCoordinates(2, 2), 6);
        sut.AddHex(hex1);
        sut.AddHex(hex2);

        // Act
        var difference = sut.GetLevelDifference(new HexCoordinates(1, 1), new HexCoordinates(2, 2));

        // Assert
        difference.ShouldBe(-4); // 2 - 6 = -4
    }

    [Fact]
    public void GetLevelDifference_WithSameHex_ReturnsZero()
    {
        // Arrange
        var sut = new BattleMap(1, 1);
        var hex = new Hex(new HexCoordinates(1, 1), 4);
        sut.AddHex(hex);

        // Act
        var difference = sut.GetLevelDifference(new HexCoordinates(1, 1), new HexCoordinates(1, 1));

        // Assert
        difference.ShouldBe(0); // 4 - 4 = 0
    }

    [Fact]
    public void GetLevelDifference_WithNonExistentFirstHex_ThrowsArgumentException()
    {
        // Arrange
        var sut = new BattleMap(2, 2);
        var hex2 = new Hex(new HexCoordinates(2, 2), 3);
        sut.AddHex(hex2);

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => 
            sut.GetLevelDifference(new HexCoordinates(1, 1), new HexCoordinates(2, 2)));
        exception.Message.ShouldContain("Hex not found at coordinates");
        exception.ParamName.ShouldBe("firstHex");
    }

    [Fact]
    public void GetLevelDifference_WithNonExistentSecondHex_ThrowsArgumentException()
    {
        // Arrange
        var sut = new BattleMap(2, 2);
        var hex1 = new Hex(new HexCoordinates(1, 1), 5);
        sut.AddHex(hex1);

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => 
            sut.GetLevelDifference(new HexCoordinates(1, 1), new HexCoordinates(2, 2)));
        exception.Message.ShouldContain("Hex not found at coordinates");
        exception.ParamName.ShouldBe("secondHex");
    }

    [Fact]
    public void GetLevelDifference_IsConsistentWithDirectHexMethod()
    {
        // Arrange
        var sut = new BattleMap(2, 2);
        var hex1 = new Hex(new HexCoordinates(1, 1), 7);
        var hex2 = new Hex(new HexCoordinates(2, 2), 3);
        sut.AddHex(hex1);
        sut.AddHex(hex2);

        // Act
        var battleMapDifference = sut.GetLevelDifference(new HexCoordinates(1, 1), new HexCoordinates(2, 2));
        var directDifference = hex1.GetLevelDifference(hex2);

        // Assert
        battleMapDifference.ShouldBe(directDifference);
    }

    [Fact]
    public void GetLevelDifference_WorksWithNegativeLevels()
    {
        // Arrange
        var sut = new BattleMap(2, 2);
        var hex1 = new Hex(new HexCoordinates(1, 1), -2);
        var hex2 = new Hex(new HexCoordinates(2, 2), -5);
        sut.AddHex(hex1);
        sut.AddHex(hex2);

        // Act
        var difference = sut.GetLevelDifference(new HexCoordinates(1, 1), new HexCoordinates(2, 2));

        // Assert
        difference.ShouldBe(3); // -2 - (-5) = 3
    }
    
    [Fact]
    public void GetHexEdges_ReturnsSixEdges()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(3, 3,
            new SingleTerrainGenerator(3, 3, new ClearTerrain()));
        var coordinates = new HexCoordinates(2, 2);

        // Act
        var edges = sut.GetHexEdges(coordinates);

        // Assert
        edges.Count.ShouldBe(6);
        edges.Select(e => e.Direction).ShouldBe(HexDirectionExtensions.AllDirections);
    }

    [Fact]
    public void GetHexEdges_ReturnsCorrectCoordinates()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(3, 3,
            new SingleTerrainGenerator(3, 3, new ClearTerrain()));
        var coordinates = new HexCoordinates(2, 2);

        // Act
        var edges = sut.GetHexEdges(coordinates);

        // Assert
        edges.ShouldAllBe(e => e.Coordinates == coordinates);
    }

    [Fact]
    public void GetHexEdges_WithSameLevel_ReturnsZeroElevationDifference()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(3, 3,
            new SingleTerrainGenerator(3, 3, new ClearTerrain()));
        var coordinates = new HexCoordinates(2, 2);

        // Act
        var edges = sut.GetHexEdges(coordinates);

        // Assert
        edges.ShouldAllBe(e => e.ElevationDifference == 0);
    }

    [Fact]
    public void GetHexEdges_WithHigherNeighbor_ReturnsNegativeElevationDifference()
    {
        // Arrange
        var sut = new BattleMap(2, 2);
        var centerHex = new Hex(new HexCoordinates(1, 1));
        var higherNeighbor = new Hex(new HexCoordinates(1, 2), 3);
        sut.AddHex(centerHex);
        sut.AddHex(higherNeighbor);

        // Act
        var edges = sut.GetHexEdges(new HexCoordinates(1, 1));

        // Assert
        var topEdge = edges.First(e => e.Direction == HexDirection.Bottom);
        topEdge.ElevationDifference.ShouldBe(-3); // 0 - 3 = -3
    }

    [Fact]
    public void GetHexEdges_WithLowerNeighbor_ReturnsPositiveElevationDifference()
    {
        // Arrange
        var sut = new BattleMap(2, 2);
        var centerHex = new Hex(new HexCoordinates(1, 1), 5);
        var lowerNeighbor = new Hex(new HexCoordinates(1, 2), 2);
        sut.AddHex(centerHex);
        sut.AddHex(lowerNeighbor);

        // Act
        var edges = sut.GetHexEdges(new HexCoordinates(1, 1));

        // Assert
        var topEdge = edges.First(e => e.Direction == HexDirection.Bottom);
        topEdge.ElevationDifference.ShouldBe(3); // 5 - 2 = 3
    }

    [Fact]
    public void GetHexEdges_AtMapBoundary_ReturnsZeroElevationDifferenceForMissingNeighbors()
    {
        // Arrange
        var sut = BattleMapFactory.GenerateMap(2, 2,
            new SingleTerrainGenerator(2, 2, new ClearTerrain()));
        // Make the corner raised 
        sut.AddHex(new Hex(new HexCoordinates(1, 1), level: 2));
        var cornerCoordinates = new HexCoordinates(1, 1);

        // Act
        var edges = sut.GetHexEdges(cornerCoordinates);

        // Assert
        // Corner hex has neighbors outside map boundaries - those should have elevation difference 0
        edges.Count.ShouldBe(6);
        edges
            .Where(e => sut.GetHex(cornerCoordinates.GetNeighbour(e.Direction)) == null)
            .ShouldAllBe(e => e.ElevationDifference == 0);
        edges
            .Where(e => sut.GetHex(cornerCoordinates.GetNeighbour(e.Direction)) != null)
            .ShouldAllBe(e => e.ElevationDifference == 2);
    }

    [Fact]
    public void GetHexEdges_WithNonExistentHex_ReturnsEmptyList()
    {
        // Arrange
        var sut = new BattleMap(2, 2);
        sut.AddHex(new Hex(new HexCoordinates(1, 1)));
        var nonExistentCoords = new HexCoordinates(2, 2);

        // Act
        var edges = sut.GetHexEdges(nonExistentCoords);

        // Assert
        edges.ShouldBeEmpty();
    }

    [Fact]
    public void GetHexEdges_WithMixedElevations_ReturnsCorrectDifferences()
    {
        // Arrange
        var sut = new BattleMap(3, 3);
        var centerHex = new Hex(new HexCoordinates(2, 2), 3);
        sut.AddHex(centerHex);

        // Add neighbors with different levels
        var topNeighbor = new Hex(new HexCoordinates(2, 1), 5);    // Higher
        var bottomNeighbor = new Hex(new HexCoordinates(2, 3), 1); // Lower
        var topRightNeighbor = new Hex(new HexCoordinates(3, 1), 3); // Same
        sut.AddHex(topNeighbor);
        sut.AddHex(bottomNeighbor);
        sut.AddHex(topRightNeighbor);

        // Act
        var edges = sut.GetHexEdges(new HexCoordinates(2, 2));

        // Assert
        var topEdge = edges.First(e => e.Direction == HexDirection.Top);
        topEdge.ElevationDifference.ShouldBe(-2); // 3 - 5 = -2

        var bottomEdge = edges.First(e => e.Direction == HexDirection.Bottom);
        bottomEdge.ElevationDifference.ShouldBe(2); // 3 - 1 = 2

        var topRightEdge = edges.First(e => e.Direction == HexDirection.TopRight);
        topRightEdge.ElevationDifference.ShouldBe(0); // 3 - 3 = 0
    }

    [Fact]
    public void GetLineOfSight_Unblocked_IncludesTargetHexInPath()
    {
        // Arrange
        var sut = new BattleMapFactory()
            .GenerateMap(1, 5, new SingleTerrainGenerator(1, 5, new ClearTerrain()));
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(1, 5);
        
        // Act
        var result = sut.GetLineOfSight(from, to, 2, 2);

        // Assert
        result.HasLineOfSight.ShouldBeTrue();
        result.HexPath.Count.ShouldBe(4, "Path should include all intervening hexes plus target hex");
        
        // Verify the last hex in the path is the target hex
        var lastHexInfo = result.HexPath[^1];
        lastHexInfo.Hex.Coordinates.ShouldBe(to, "Last hex in path should be the target hex");
        
        // Verify the target hex has the correct interpolated height (target level + target height = 2 + 2 = 4)
        lastHexInfo.InterpolatedLosHeight.ShouldBe(4);
        
        // Clear terrain has ceiling 0, which is less than LOS height 4, so the contribution should be 0
        lastHexInfo.InterveningFactor.ShouldBe(0, "Clear terrain should not contribute to intervening factor when below LOS line");
    }

    [Fact]
    public void GetLineOfSight_Unblocked_WithTargetWoods_IncludesTargetContribution()
    {
        // Arrange
        var sut = new BattleMapFactory()
            .GenerateMap(1,3, new SingleTerrainGenerator(1,3, new ClearTerrain()));
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(1, 3);
        
        // Add heavy woods to target hex (ceiling = 2, intervening factor = 2)
        var targetHex = sut.GetHex(to)!;
        targetHex.AddTerrain(new HeavyWoodsTerrain());

        // Act - Attacker height 2, target height 2, LOS line at target = 0 + 2 = 2
        var result = sut.GetLineOfSight(from, to, 2, 2);

        // Assert
        result.HasLineOfSight.ShouldBeTrue();
        result.HexPath.Count.ShouldBe(2, "Path should include intervening hex plus target hex");
        
        // Find the target hex in a path
        var targetHexInfo = result.HexPath[^1];
        targetHexInfo.Hex.Coordinates.ShouldBe(to);
        
        // Heavy woods ceiling (2) >= LOS height (2), so it contributes
        targetHexInfo.InterveningFactor.ShouldBe(2, "Heavy woods at target should contribute intervening factor");
        result.TotalInterveningFactor.ShouldBe(0); // but the target hex doesn't contribute to the total factor
    }
}
