using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Movement;

public class MovementTypeExtensionsTests
{
    [Theory]
    [InlineData(MovementType.Walk, PhysicalAttackType.Charge)]
    [InlineData(MovementType.Run, PhysicalAttackType.Charge)]
    [InlineData(MovementType.Jump, PhysicalAttackType.DFA)]
    public void GetMovementAttackType_MapsMovementTypesToMovementAttacks(
        MovementType movementType,
        PhysicalAttackType expected)
    {
        movementType.GetMovementAttackType().ShouldBe(expected);
    }

    [Theory]
    [InlineData(MovementType.StandingStill)]
    public void GetMovementAttackType_ForMovementWithoutAttack_ReturnsNull(MovementType movementType)
    {
        movementType.GetMovementAttackType().ShouldBeNull();
    }
}
