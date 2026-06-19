using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Data;

public class ReachableAreaTest
{
    private static HexReachabilityData H(HexCoordinates coords, HexSurface surface = HexSurface.Ground, int cost = 0)
        => new(coords, surface, cost);

    [Fact]
    public void IsForwardReachable_ReturnsTrue_WhenHexIsForwardReachable()
    {
        // Arrange
        var forwardReachableHexes = new[] { H(new HexCoordinates(1, 1)) };
        var sut = new ReachableArea(forwardReachableHexes, []);

        // Act & Assert
        sut.IsForwardReachable(new HexCoordinates(1, 1)).ShouldBeTrue();
    }
    
    [Fact]
    public void IsForwardReachable_ReturnsFalse_WhenHexIsNotForwardReachable()
    {
        // Arrange
        var forwardReachableHexes = new[] { H(new HexCoordinates(1, 1)) };
        var sut = new ReachableArea(forwardReachableHexes, []);

        // Act & Assert
        sut.IsForwardReachable(new HexCoordinates(2, 2)).ShouldBeFalse();
    }
    
    [Fact]
    public void IsBackwardReachable_ReturnsTrue_WhenHexIsBackwardReachable()
    {
        // Arrange
        var backwardReachableHexes = new[] { H(new HexCoordinates(1, 1)) };
        var sut = new ReachableArea([], backwardReachableHexes);

        // Act & Assert
        sut.IsBackwardReachable(new HexCoordinates(1, 1)).ShouldBeTrue();
    }
    
    [Fact]
    public void IsBackwardReachable_ReturnsFalse_WhenHexIsNotBackwardReachable()
    {
        // Arrange
        var backwardReachableHexes = new[] { H(new HexCoordinates(1, 1)) };
        var sut = new ReachableArea([], backwardReachableHexes);

        // Act & Assert
        sut.IsBackwardReachable(new HexCoordinates(2, 2)).ShouldBeFalse();
    }
    
    [Fact]
    public void IsHexReachable_ReturnsTrue_WhenHexIsForwardReachable()
    {
        // Arrange
        var forwardReachableHexes = new[] { H(new HexCoordinates(1, 1)) };
        var sut = new ReachableArea(forwardReachableHexes, []);

        // Act & Assert
        sut.IsHexReachable(new HexCoordinates(1, 1)).ShouldBeTrue();
    }
    
    [Fact]
    public void IsHexReachable_ReturnsTrue_WhenHexIsBackwardReachable()
    {
        // Arrange
        var backwardReachableHexes = new[] { H(new HexCoordinates(1, 1)) };
        var sut = new ReachableArea([], backwardReachableHexes);

        // Act & Assert
        sut.IsHexReachable(new HexCoordinates(1, 1)).ShouldBeTrue();
    }
    
    [Fact]
    public void IsHexReachable_ReturnsFalse_WhenHexIsNotReachable()
    {
        // Arrange
        var forwardReachableHexes = new[] { H(new HexCoordinates(1, 1)) };
        var backwardReachableHexes = new[] { H(new HexCoordinates(2, 2)) };
        var sut = new ReachableArea(forwardReachableHexes, backwardReachableHexes);

        // Act & Assert
        sut.IsHexReachable(new HexCoordinates(3, 3)).ShouldBeFalse();
    }
    
    [Fact]
    public void AllReachableHexes_ReturnsUnionOfForwardAndBackwardReachableHexes()
    {
        // Arrange
        var forwardReachableHexes = new[]
        {
            H(new HexCoordinates(1, 1)), H(new HexCoordinates(2, 2))
        };
        var backwardReachableHexes = new[]
        {
            H(new HexCoordinates(3, 3)), H(new HexCoordinates(4, 4))
        };
        var sut = new ReachableArea(forwardReachableHexes, backwardReachableHexes);

        // Act & Assert
        sut.AllReachableHexes.Select(x => x.Coordinates).ShouldBe([
            new HexCoordinates(1, 1),
            new HexCoordinates(2, 2),
            new HexCoordinates(3, 3),
            new HexCoordinates(4, 4)]);
    }
    
    [Fact]
    public void AllReachableHexes_ReturnsEmptyList_WhenNoHexesAreReachable()    
    {
        // Arrange
        var sut = new ReachableArea([], []);

        // Act & Assert
        sut.AllReachableHexes.ShouldBeEmpty();
    }
    
    [Fact]
    public void AllReachableHexes_ReturnsForwardReachableHexes_WhenNoBackwardReachableHexes()
    {
        // Arrange
        var forwardReachableHexes = new[]
        {
            H(new HexCoordinates(1, 1)), H(new HexCoordinates(2, 2))
        };
        var sut = new ReachableArea(forwardReachableHexes, []);

        // Act & Assert
        sut.AllReachableHexes.Select(x => x.Coordinates).ShouldBe(
            forwardReachableHexes.Select(x => x.Coordinates));
    }
    
    [Fact]
    public void AllReachableHexes_ReturnsBackwardReachableHexes_WhenNoForwardReachableHexes()
    {
        // Arrange
        var backwardReachableHexes = new[]
        {
            H(new HexCoordinates(3, 3)), H(new HexCoordinates(4, 4))
        };
        var sut = new ReachableArea([], backwardReachableHexes);

        // Act & Assert
        sut.AllReachableHexes.Select(x => x.Coordinates).ShouldBe(
            backwardReachableHexes.Select(x => x.Coordinates));
    }
    
    [Fact]
    public void AllReachableHexes_ReturnsUniqueHexes_WhenForwardAndBackwardReachableHexesOverlap()
    {
        // Arrange
        var forwardReachableHexes = new[]
        {
            H(new HexCoordinates(1, 1)), H(new HexCoordinates(2, 2))
        };
        var backwardReachableHexes = new[]
        {
            H(new HexCoordinates(2, 2)), H(new HexCoordinates(3, 3))
        };
        var sut = new ReachableArea(forwardReachableHexes, backwardReachableHexes);

        // Act & Assert
        sut.AllReachableHexes.Select(x => x.Coordinates).ShouldBe([
            new HexCoordinates(1, 1), 
            new HexCoordinates(2, 2), 
            new HexCoordinates(3, 3)]);
    }
    
    [Fact]
    public void AllReachableCoordinates_ReturnsUnionOfForwardAndBackwardCoordinates()
    {
        var forwardReachableHexes = new[]
        {
            H(new HexCoordinates(1, 1)), H(new HexCoordinates(2, 2))
        };
        var backwardReachableHexes = new[]
        {
            H(new HexCoordinates(3, 3)), H(new HexCoordinates(4, 4))
        };
        var sut = new ReachableArea(forwardReachableHexes, backwardReachableHexes);

        sut.AllReachableCoordinates.ShouldBe([
            new HexCoordinates(1, 1),
            new HexCoordinates(2, 2),
            new HexCoordinates(3, 3),
            new HexCoordinates(4, 4)], ignoreOrder: true);
    }
    
    [Fact]
    public void AllReachableCoordinates_ReturnsEmptySet_WhenNoHexesAreReachable()
    {
        var sut = new ReachableArea([], []);

        sut.AllReachableCoordinates.ShouldBeEmpty();
    }
    
    [Fact]
    public void AllReachableCoordinates_ReturnsForwardCoordinates_WhenNoBackwardReachableHexes()
    {
        var forwardReachableHexes = new[]
        {
            H(new HexCoordinates(1, 1)), H(new HexCoordinates(2, 2))
        };
        var sut = new ReachableArea(forwardReachableHexes, []);

        sut.AllReachableCoordinates.ShouldBe([
            new HexCoordinates(1, 1),
            new HexCoordinates(2, 2)], ignoreOrder: true);
    }
    
    [Fact]
    public void AllReachableCoordinates_ReturnsBackwardCoordinates_WhenNoForwardReachableHexes()
    {
        var backwardReachableHexes = new[]
        {
            H(new HexCoordinates(3, 3)), H(new HexCoordinates(4, 4))
        };
        var sut = new ReachableArea([], backwardReachableHexes);

        sut.AllReachableCoordinates.ShouldBe([
            new HexCoordinates(3, 3),
            new HexCoordinates(4, 4)], ignoreOrder: true);
    }
    
    [Fact]
    public void AllReachableCoordinates_ReturnsUniqueCoordinates_WhenForwardAndBackwardOverlap()
    {
        var forwardReachableHexes = new[]
        {
            H(new HexCoordinates(1, 1)), H(new HexCoordinates(2, 2))
        };
        var backwardReachableHexes = new[]
        {
            H(new HexCoordinates(2, 2)), H(new HexCoordinates(3, 3))
        };
        var sut = new ReachableArea(forwardReachableHexes, backwardReachableHexes);

        sut.AllReachableCoordinates.ShouldBe([
            new HexCoordinates(1, 1), 
            new HexCoordinates(2, 2), 
            new HexCoordinates(3, 3)], ignoreOrder: true);
    }
    
    [Fact]
    public void GetReachableSurfacesForCoordinate_ReturnsSurfaces_WhenCoordinateFoundInForward()
    {
        var forward = new[] { H(new HexCoordinates(1, 1)), H(new HexCoordinates(1, 1), HexSurface.Bridge, 5) };
        var sut = new ReachableArea(forward, []);

        var result = sut.GetReachableSurfacesForCoordinate(new HexCoordinates(1, 1)).ToList();

        result.Count.ShouldBe(2);
        result.ShouldContain(r => r.Surface == HexSurface.Ground && r.Cost == 0);
        result.ShouldContain(r => r.Surface == HexSurface.Bridge && r.Cost == 5);
    }

    [Fact]
    public void GetReachableSurfacesForCoordinate_ReturnsSurfaces_WhenCoordinateFoundInBackward()
    {
        var backward = new[] { H(new HexCoordinates(1, 1)), H(new HexCoordinates(1, 1), HexSurface.Bridge, 4) };
        var sut = new ReachableArea([], backward);

        var result = sut.GetReachableSurfacesForCoordinate(new HexCoordinates(1, 1)).ToList();

        result.Count.ShouldBe(2);
        result.ShouldContain(r => r.Surface == HexSurface.Ground);
        result.ShouldContain(r => r.Surface == HexSurface.Bridge);
    }

    [Fact]
    public void GetReachableSurfacesForCoordinate_CombinesForwardAndBackward()
    {
        var forward = new[] { H(new HexCoordinates(1, 1), HexSurface.Ground, 3) };
        var backward = new[] { H(new HexCoordinates(1, 1), HexSurface.Bridge, 2) };
        var sut = new ReachableArea(forward, backward);

        var result = sut.GetReachableSurfacesForCoordinate(new HexCoordinates(1, 1)).ToList();

        result.Count.ShouldBe(2);
    }

    [Fact]
    public void GetReachableSurfacesForCoordinate_ReturnsEmpty_WhenCoordinateNotFound()
    {
        var forward = new[] { H(new HexCoordinates(2, 2)) };
        var sut = new ReachableArea(forward, []);

        var result = sut.GetReachableSurfacesForCoordinate(new HexCoordinates(1, 1));

        result.ShouldBeEmpty();
    }
}