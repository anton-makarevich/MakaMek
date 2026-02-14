using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Map;

public class LineOfSightCacheTests
{
    [Fact]
    public void TryGetPath_NoCache_ReturnsFalse()
    {
        // Arrange
        var cache = new LineOfSightCache();
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(3, 3);

        // Act
        var result = cache.TryGetPath(from, to, out var path);

        // Assert
        result.ShouldBeFalse();
        path.ShouldBeNull();
    }

    [Fact]
    public void TryGetPath_DirectPath_ReturnsTrue()
    {
        // Arrange
        var cache = new LineOfSightCache();
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(3, 3);
        var expectedPath = new List<HexCoordinates> 
        { 
            new(1, 1), 
            new(2, 2), 
            new(3, 3) 
        };
        cache.AddPath(from, to, expectedPath);

        // Act
        var result = cache.TryGetPath(from, to, out var path);

        // Assert
        result.ShouldBeTrue();
        path.ShouldBe(expectedPath);
    }

    [Fact]
    public void TryGetPath_ReversedPath_ReturnsReversedList()
    {
        // Arrange
        var cache = new LineOfSightCache();
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(3, 3);
        var originalPath = new List<HexCoordinates> 
        { 
            new(1, 1), 
            new(2, 2), 
            new(3, 3) 
        };
        cache.AddPath(from, to, originalPath);

        // Act
        var result = cache.TryGetPath(to, from, out var path);

        // Assert
        result.ShouldBeTrue();
        path.ShouldNotBeNull();
        path.Count.ShouldBe(3);
        path[0].ShouldBe(new HexCoordinates(3, 3));
        path[1].ShouldBe(new HexCoordinates(2, 2));
        path[2].ShouldBe(new HexCoordinates(1, 1));
    }

    [Fact]
    public void Clear_RemovesAllPaths()
    {
        // Arrange
        var cache = new LineOfSightCache();
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(3, 3);
        var path = new List<HexCoordinates> { new(1, 1), new(2, 2), new(3, 3) };
        cache.AddPath(from, to, path);

        // Act
        cache.Clear();

        // Assert
        cache.TryGetPath(from, to, out _).ShouldBeFalse();
        cache.TryGetPath(to, from, out _).ShouldBeFalse();
    }
}
