using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Mechanics.PhysicalAttacks;

public class PhysicalAttackDeclarationExtensionsTests
{
    [Fact]
    public void IsMovementAttack_ReturnsTrue_ForChargeAndDfa()
    {
        Declaration(PhysicalAttackType.Charge).IsMovementAttack().ShouldBeTrue();
        Declaration(PhysicalAttackType.DFA).IsMovementAttack().ShouldBeTrue();
    }

    [Fact]
    public void IsMovementAttack_ReturnsFalse_ForNullAndOtherPhysicalAttacks()
    {
        ((PhysicalAttackDeclarationData?)null).IsMovementAttack().ShouldBeFalse();
        Declaration(PhysicalAttackType.Punch).IsMovementAttack().ShouldBeFalse();
        Declaration(PhysicalAttackType.Kick).IsMovementAttack().ShouldBeFalse();
        Declaration(PhysicalAttackType.Push).IsMovementAttack().ShouldBeFalse();
    }

    private static PhysicalAttackDeclarationData Declaration(PhysicalAttackType attackType) =>
        new()
        {
            AttackType = attackType,
            AttackerLimbs = [],
            TargetId = Guid.NewGuid()
        };
}
