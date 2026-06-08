using JetBrains.Annotations;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

[TestSubject(typeof(MovementPathCacheKey))]
public class MovementPathCacheKeyTest
{

    [Fact]
    public void MovementPathCacheKey_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var start = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var destination = new HexPosition(new HexCoordinates(2, 2), HexDirection.Top);
        var sut = new MovementPathCacheKey(start, destination, true, 2, 1);
        
        // Assert
        sut.Start.ShouldBe(start);
        sut.Destination.ShouldBe(destination);
        sut.IsJump.ShouldBe(true);
        sut.MaxLevelChange.ShouldBe(2);
        sut.UnitHeight.ShouldBe(1);
    }
}