using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.PhysicalAttack;

public class PhysicalAttackTargetExtensionsTests
{
    [Theory]
    // A 'Mech making a Death From Above cannot be the target of any physical attack (p.149).
    [InlineData(PhysicalAttackType.DFA, PhysicalAttackType.Punch,
        PhysicalAttackRestriction.TargetPerformingDeathFromAbove)]
    [InlineData(PhysicalAttackType.DFA, PhysicalAttackType.Kick,
        PhysicalAttackRestriction.TargetPerformingDeathFromAbove)]
    [InlineData(PhysicalAttackType.DFA, PhysicalAttackType.Push,
        PhysicalAttackRestriction.TargetPerformingDeathFromAbove)]
    [InlineData(PhysicalAttackType.DFA, PhysicalAttackType.DFA,
        PhysicalAttackRestriction.TargetPerformingDeathFromAbove)]
    // A 'Mech making a Charge cannot be pushed or attacked by another movement attack (p.148-149).
    [InlineData(PhysicalAttackType.Charge, PhysicalAttackType.Push,
        PhysicalAttackRestriction.TargetPerformingDisplacementAttack)]
    [InlineData(PhysicalAttackType.Charge, PhysicalAttackType.Charge,
        PhysicalAttackRestriction.TargetPerformingDisplacementAttack)]
    [InlineData(PhysicalAttackType.Charge, PhysicalAttackType.DFA,
        PhysicalAttackRestriction.TargetPerformingDisplacementAttack)]
    public void GetPendingAttackRestriction_ReportsThePendingAttack(PhysicalAttackType pendingAttack,
        PhysicalAttackType attackType, PhysicalAttackRestriction expected)
    {
        var target = CreateTargetWithPendingAttack(pendingAttack);

        target.GetPendingAttackRestriction(attackType).ShouldBe(expected);
    }

    [Theory]
    [InlineData(PhysicalAttackType.Charge, PhysicalAttackType.Punch)]
    [InlineData(PhysicalAttackType.Charge, PhysicalAttackType.Kick)]
    public void GetPendingAttackRestriction_AllowsPunchAndKickAgainstAChargingTarget(
        PhysicalAttackType pendingAttack, PhysicalAttackType attackType)
    {
        var target = CreateTargetWithPendingAttack(pendingAttack);

        target.GetPendingAttackRestriction(attackType).ShouldBeNull();
    }

    [Fact]
    public void GetPendingAttackRestriction_WithoutAPendingAttack_ReturnsNull()
    {
        var target = Substitute.For<IUnit>();

        foreach (var attackType in Enum.GetValues<PhysicalAttackType>())
            target.GetPendingAttackRestriction(attackType).ShouldBeNull();
    }

    private static IUnit CreateTargetWithPendingAttack(PhysicalAttackType pendingAttack)
    {
        var target = Substitute.For<IUnit>();
        target.DeclaredPhysicalAttack.Returns(new PhysicalAttackDeclarationData
        {
            AttackType = pendingAttack,
            AttackerLimbs = [],
            TargetId = Guid.NewGuid()
        });
        return target;
    }
}
