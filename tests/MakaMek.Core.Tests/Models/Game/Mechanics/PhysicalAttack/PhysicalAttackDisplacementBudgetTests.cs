using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.PhysicalAttack;

public class PhysicalAttackDisplacementBudgetTests
{
    [Theory]
    [InlineData(PhysicalAttackType.Push)]
    [InlineData(PhysicalAttackType.Charge)]
    [InlineData(PhysicalAttackType.DFA)]
    public void IsReserved_WhenADisplacementAttackTargetsTheUnit_IsTrue(PhysicalAttackType attackType)
    {
        var targetId = Guid.NewGuid();
        var unit = UnitDeclaring(attackType, targetId);

        PhysicalAttackDisplacementBudget.IsReserved(targetId, [unit]).ShouldBeTrue();
    }

    [Theory]
    [InlineData(PhysicalAttackType.Punch)]
    [InlineData(PhysicalAttackType.Kick)]
    public void IsReserved_WhenOnlyANonDisplacementAttackTargetsTheUnit_IsFalse(PhysicalAttackType attackType)
    {
        var targetId = Guid.NewGuid();
        var unit = UnitDeclaring(attackType, targetId);

        PhysicalAttackDisplacementBudget.IsReserved(targetId, [unit]).ShouldBeFalse();
    }

    [Fact]
    public void IsReserved_ForAnotherTarget_IsFalse()
    {
        var unit = UnitDeclaring(PhysicalAttackType.Push, Guid.NewGuid());

        PhysicalAttackDisplacementBudget.IsReserved(Guid.NewGuid(), [unit]).ShouldBeFalse();
    }

    [Fact]
    public void IsReserved_WithNoDeclarations_IsFalse()
    {
        var unit = Substitute.For<IUnit>();
        unit.DeclaredPhysicalAttack.Returns((PhysicalAttackDeclarationData?)null);

        PhysicalAttackDisplacementBudget.IsReserved(Guid.NewGuid(), [unit]).ShouldBeFalse();
    }

    private static IUnit UnitDeclaring(PhysicalAttackType attackType, Guid targetId)
    {
        var unit = Substitute.For<IUnit>();
        unit.DeclaredPhysicalAttack.Returns(new PhysicalAttackDeclarationData
        {
            AttackType = attackType,
            AttackerLimbs = [PartLocation.LeftArm],
            TargetId = targetId
        });
        return unit;
    }
}
