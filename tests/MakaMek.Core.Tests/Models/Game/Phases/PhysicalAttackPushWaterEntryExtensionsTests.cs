using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Phases;

public class PhysicalAttackPushWaterEntryExtensionsTests
{
    [Fact]
    public void ApplyPushWaterEntryModifier_UpdatesTheReferencedRoll_WhenTwoPushRollsHaveEqualValues()
    {
        // Arrange: two pushes against the same unit in one phase produce two PushHit contexts with
        // identical values, so the modifier has to be located by instance, not by value equality.
        var unitId = Guid.NewGuid();
        var coordinates = new HexCoordinates(3, 3);
        var unit = Substitute.For<IUnit>();
        unit.Id.Returns(unitId);
        unit.Position.Returns(new HexPosition(coordinates, HexDirection.Top));

        var waterHex = new Hex(coordinates);
        waterHex.AddTerrain(new WaterTerrain(-1));
        var map = Substitute.For<IBattleMap>();
        map.GetHex(coordinates).Returns(waterHex);

        var firstPush = new PushHitRollContext();
        var secondPush = new PushHitRollContext();
        List<PilotingSkillRollContext> contexts = [firstPush, secondPush];
        var state = new PhysicalAttackPhaseState();
        state.PushHitContexts[unitId] = (contexts, secondPush);

        // Act
        unit.ApplyPushWaterEntryModifier(map, state);

        // Assert: only the second push entered water, so only its own roll carries the modifier.
        ((PushHitRollContext)contexts[0]).WaterDepth.ShouldBe(0);
        ((PushHitRollContext)contexts[1]).WaterDepth.ShouldBe(1);
    }
}
