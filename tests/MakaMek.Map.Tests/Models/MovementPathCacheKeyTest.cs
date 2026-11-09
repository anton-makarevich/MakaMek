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

    [Fact]
    public void MovementPathCacheKey_ShouldDefaultOptionalFields()
    {
        // Arrange & Act - callers that do not care about jump profile details use the defaults
        var sut = new MovementPathCacheKey(null, null, false, null, 1);

        // Assert - resetting the defaults would silently break cache lookups
        sut.JumpMovementPoints.ShouldBeNull();
        sut.ApproachWaypointOffset.ShouldBe(0);
    }

    [Fact]
    public void MovementPathCacheKey_ShouldDifferentiateByApproachWaypointOffset()
    {
        // Arrange - a jump profile computed with a different airborne offset must not collide
        var key = new MovementPathCacheKey(null, null, true, null, 1, JumpMovementPoints: 4);

        // Act
        var keyWithOffset = key with { ApproachWaypointOffset = 2 };

        // Assert
        key.ApproachWaypointOffset.ShouldBe(0);
        keyWithOffset.ApproachWaypointOffset.ShouldBe(2);
        keyWithOffset.ShouldNotBe(key);
    }
}