using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics;

public class PhysicalAttackDefinitionsTests
{
    [Fact]
    public void Supported_ContainsPunchKickAndPush()
    {
        PhysicalAttackDefinitions.Supported
            .Select(definition => definition.AttackType)
            .ShouldBe([PhysicalAttackType.Punch, PhysicalAttackType.Kick, PhysicalAttackType.Push], ignoreOrder: true);
    }

    [Theory]
    [InlineData(PhysicalAttackType.Punch, PartLocation.LeftArm, PartLocation.RightArm, 2, 1, false)]
    [InlineData(PhysicalAttackType.Kick, PartLocation.LeftLeg, PartLocation.RightLeg, 1, 1, false)]
    [InlineData(PhysicalAttackType.Push, PartLocation.LeftArm, PartLocation.RightArm, 2, 2, true)]
    public void Get_ReturnsTheStructuralMetadataOfTheAttack(
        PhysicalAttackType attackType,
        PartLocation firstLimb,
        PartLocation secondLimb,
        int maxLimbsPerDeclaration,
        int minLimbsPerDeclaration,
        bool resolvesAsSingleAttack)
    {
        var definition = PhysicalAttackDefinitions.Get(attackType);

        definition.AttackType.ShouldBe(attackType);
        definition.CandidateLimbs.ShouldBe([firstLimb, secondLimb]);
        definition.MaxLimbsPerDeclaration.ShouldBe(maxLimbsPerDeclaration);
        definition.MinLimbsPerDeclaration.ShouldBe(minLimbsPerDeclaration);
        definition.ResolvesAsSingleAttack.ShouldBe(resolvesAsSingleAttack);
    }

    [Fact]
    public void TryGet_WithASupportedAttack_ReturnsTheDefinition()
    {
        PhysicalAttackDefinitions.TryGet(PhysicalAttackType.Punch, out var definition).ShouldBeTrue();

        definition.ShouldNotBeNull();
        definition.AttackType.ShouldBe(PhysicalAttackType.Punch);
    }

    [Theory]
    [InlineData(PhysicalAttackType.Charge)]
    [InlineData(PhysicalAttackType.DFA)]
    public void TryGet_WithAnUnsupportedAttack_ReturnsFalse(PhysicalAttackType attackType)
    {
        PhysicalAttackDefinitions.TryGet(attackType, out var definition).ShouldBeFalse();

        definition.ShouldBeNull();
    }

    [Theory]
    [InlineData(PhysicalAttackType.Charge)]
    [InlineData(PhysicalAttackType.DFA)]
    public void Get_WithAnUnsupportedAttack_Throws(PhysicalAttackType attackType)
    {
        var exception = Should.Throw<NotSupportedException>(() => PhysicalAttackDefinitions.Get(attackType));

        exception.Message.ShouldBe(
            $"Physical attack resolution is not supported for attack type: {attackType}");
    }

    [Theory]
    [InlineData(PhysicalAttackType.Push, true)]
    [InlineData(PhysicalAttackType.Charge, true)]
    [InlineData(PhysicalAttackType.DFA, true)]
    [InlineData(PhysicalAttackType.Punch, false)]
    [InlineData(PhysicalAttackType.Kick, false)]
    public void IsDisplacementAttack_ClassifiesPushChargeAndDfa(PhysicalAttackType attackType, bool expected)
    {
        PhysicalAttackDefinitions.IsDisplacementAttack(attackType).ShouldBe(expected);
    }
}
