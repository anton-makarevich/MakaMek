using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units;

public class IsSkiddingTests
{
    [Fact]
    public void IsSkidding_ReturnsFalse_WhenMovementTakenIsNull()
    {
        var sut = CreateUnit();

        sut.IsSkidding.ShouldBeFalse();
    }

    [Fact]
    public void IsSkidding_ReturnsFalse_WhenMovementPathHasNoSkidEvents()
    {
        var sut = CreateUnit();
        var position = new HexPosition(1, 1, HexDirection.Top);
        sut.Deploy(position, null);

        var path = new MovementPath(
        [
            new PathSegment(position, new HexPosition(2, 1, HexDirection.Top),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        ], MovementType.Walk);
        sut.Move(path, null, true);

        sut.IsSkidding.ShouldBeFalse();
    }

    [Fact]
    public void IsSkidding_ReturnsTrue_WhenMovementPathContainsSkidEvent()
    {
        var sut = CreateUnit();
        var position = new HexPosition(1, 1, HexDirection.Top);
        sut.Deploy(position, null);

        var path = new MovementPath(
        [
            new PathSegment(position, new HexPosition(2, 1, HexDirection.Top),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }],
                Events: [new SegmentEvent(SegmentEventType.Skid)])
        ], MovementType.Walk);
        sut.Move(path, null, true);

        sut.IsSkidding.ShouldBeTrue();
    }

    private static IUnit CreateUnit()
    {
        return new UnitTests.TestUnit("Test", "Mech", 20, []);
    }
}
