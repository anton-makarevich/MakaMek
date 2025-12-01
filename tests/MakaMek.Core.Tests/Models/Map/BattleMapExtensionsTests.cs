using Sanet.MakaMek.Core.Models.Map;
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
}
