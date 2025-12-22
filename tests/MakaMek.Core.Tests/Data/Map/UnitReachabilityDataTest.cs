using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Models.Map;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Map;

public class UnitReachabilityDataTest
{
    [Fact]
    public void IsForwardReachable_ReturnsTrue_WhenHexIsForwardReachable()
    {
        // Arrange
        List<HexCoordinates> forwardReachableHexes = [new(1, 1)];
        var sut = new UnitReachabilityData(forwardReachableHexes,[]);

        // Act & Assert
        sut.IsForwardReachable(new HexCoordinates(1, 1)).ShouldBeTrue();
    }
    
    [Fact]
    public void IsForwardReachable_ReturnsFalse_WhenHexIsNotForwardReachable()
    {
        // Arrange
        List<HexCoordinates> forwardReachableHexes = [new(1, 1)];
        var sut = new UnitReachabilityData(forwardReachableHexes,[]);

        // Act & Assert
        sut.IsForwardReachable(new HexCoordinates(2, 2)).ShouldBeFalse();
    }
    
    [Fact]
    public void IsBackwardReachable_ReturnsTrue_WhenHexIsBackwardReachable()
    {
        // Arrange
        List<HexCoordinates> backwardReachableHexes = [new(1, 1)];
        var sut = new UnitReachabilityData([],backwardReachableHexes);

        // Act & Assert
        sut.IsBackwardReachable(new HexCoordinates(1, 1)).ShouldBeTrue();
    }
    
    [Fact]
    public void IsBackwardReachable_ReturnsFalse_WhenHexIsNotBackwardReachable()
    {
        // Arrange
        List<HexCoordinates> backwardReachableHexes = [new(1, 1)];
        var sut = new UnitReachabilityData([],backwardReachableHexes);

        // Act & Assert
        sut.IsBackwardReachable(new HexCoordinates(2, 2)).ShouldBeFalse();
    }
    
    [Fact]
    public void IsHexReachable_ReturnsTrue_WhenHexIsForwardReachable()
    {
        // Arrange
        List<HexCoordinates> forwardReachableHexes = [new(1, 1)];
        var sut = new UnitReachabilityData(forwardReachableHexes,[]);

        // Act & Assert
        sut.IsHexReachable(new HexCoordinates(1, 1)).ShouldBeTrue();
    }
    
    [Fact]
    public void IsHexReachable_ReturnsTrue_WhenHexIsBackwardReachable()
    {
        // Arrange
        List<HexCoordinates> backwardReachableHexes = [new(1, 1)];
        var sut = new UnitReachabilityData([],backwardReachableHexes);

        // Act & Assert
        sut.IsHexReachable(new HexCoordinates(1, 1)).ShouldBeTrue();
    }
    
    [Fact]
    public void IsHexReachable_ReturnsFalse_WhenHexIsNotReachable()
    {
        // Arrange
        List<HexCoordinates> forwardReachableHexes = [new(1, 1)];
        List<HexCoordinates> backwardReachableHexes = [new(2, 2)];
        var sut = new UnitReachabilityData(forwardReachableHexes,backwardReachableHexes);

        // Act & Assert
        sut.IsHexReachable(new HexCoordinates(3, 3)).ShouldBeFalse();
    }
    
    [Fact]
    public void AllReachableHexes_ReturnsUnionOfForwardAndBackwardReachableHexes()
    {
        // Arrange
        List<HexCoordinates> forwardReachableHexes = [new(1, 1), new(2, 2)];
        List<HexCoordinates> backwardReachableHexes = [new(3, 3), new(4, 4)];
        var sut = new UnitReachabilityData(forwardReachableHexes,backwardReachableHexes);

        // Act & Assert
        sut.AllReachableHexes.ShouldBe([
            new HexCoordinates(1, 1),
            new HexCoordinates(2, 2),
            new HexCoordinates(3, 3),
            new HexCoordinates(4, 4)]);
    }
    
    [Fact]
    public void AllReachableHexes_ReturnsEmptyList_WhenNoHexesAreReachable()    
    {
        // Arrange
        var sut = new UnitReachabilityData([],[]);

        // Act & Assert
        sut.AllReachableHexes.ShouldBeEmpty();
    }
    
    [Fact]
    public void AllReachableHexes_ReturnsForwardReachableHexes_WhenNoBackwardReachableHexes()
    {
        // Arrange
        List<HexCoordinates> forwardReachableHexes = [new(1, 1), new(2, 2)];
        var sut = new UnitReachabilityData(forwardReachableHexes,[]);

        // Act & Assert
        sut.AllReachableHexes.ShouldBe(forwardReachableHexes);
    }
    
    [Fact]
    public void AllReachableHexes_ReturnsBackwardReachableHexes_WhenNoForwardReachableHexes()
    {
        // Arrange
        List<HexCoordinates> backwardReachableHexes = [new(3, 3), new(4, 4)];
        var sut = new UnitReachabilityData([],backwardReachableHexes);

        // Act & Assert
        sut.AllReachableHexes.ShouldBe(backwardReachableHexes);
    }
    
    [Fact]
    public void AllReachableHexes_ReturnsUniqueHexes_WhenForwardAndBackwardReachableHexesOverlap()
    {
        // Arrange
        List<HexCoordinates> forwardReachableHexes = [new(1, 1), new(2, 2)];
        List<HexCoordinates> backwardReachableHexes = [new(2, 2), new(3, 3)];
        var sut = new UnitReachabilityData(forwardReachableHexes,backwardReachableHexes);

        // Act & Assert
        sut.AllReachableHexes.ShouldBe([
            new HexCoordinates(1, 1), 
            new HexCoordinates(2, 2), 
            new HexCoordinates(3, 3)]);
    }
}