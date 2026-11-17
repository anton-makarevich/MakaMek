using NSubstitute;
using Sanet.Localization;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.UiStates;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.UiStates;

public class PhysicalAttackOptionBuilderTests
{
    private readonly IClientGame _game = Substitute.For<IClientGame>();
    private readonly IPhysicalAttackEligibility _eligibility = Substitute.For<IPhysicalAttackEligibility>();
    private readonly IPhysicalAttackCalculator _calculator = Substitute.For<IPhysicalAttackCalculator>();
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IUnit _attacker = Substitute.For<IUnit>();
    private readonly IUnit _target = Substitute.For<IUnit>();
    private readonly IBattleMap _map = Substitute.For<IBattleMap>();
    private readonly IReadOnlyList<IUnit> _allUnits = [];
    private readonly PhysicalAttackOptionBuilder _sut;

    public PhysicalAttackOptionBuilderTests()
    {
        // Return the localization key so assertions can name the expected message.
        _localizationService.GetString(Arg.Any<string>())
            .Returns(call => call.Arg<string>());
        _game.BattleMap.Returns(_map);
        _game.PhysicalAttackEligibility.Returns(_eligibility);
        _game.PhysicalAttackCalculator.Returns(_calculator);
        _sut = new PhysicalAttackOptionBuilder(_game, _localizationService);
    }

    [Fact]
    public void Build_WithoutABattleMap_ReturnsNoRows()
    {
        // Arrange
        _game.BattleMap.Returns((IBattleMap?)null);

        // Act
        var result = _sut.Build(_attacker, _target, _allUnits, (_, _) => { });

        // Assert
        result.ShouldBeEmpty();
        _eligibility.DidNotReceive().GetLimbEligibility(
            Arg.Any<IUnit>(), Arg.Any<IUnit>(), Arg.Any<PhysicalAttackType>(), Arg.Any<IBattleMap>());
    }

    [Fact]
    public void Build_ForAnAvailableAttack_ComputesTheTargetNumberAndBreakdown()
    {
        // Arrange
        var breakdown = CreateBreakdown();
        StubAttack(PhysicalAttackType.Punch, [Available(PartLocation.LeftArm)], restriction: null);
        _calculator
            .GetToHitNumber(_attacker, _target, PhysicalAttackType.Punch, _map)
            .Returns(7);
        _calculator
            .GetModifierBreakdown(_attacker, _target, PhysicalAttackType.Punch, _map)
            .Returns(breakdown);

        // Act
        var result = _sut.Build(_attacker, _target, _allUnits, (_, _) => { });

        // Assert
        var punch = Option(result, PhysicalAttackType.Punch);
        punch.IsEnabled.ShouldBeTrue();
        punch.RestrictionReason.ShouldBeNull();
        punch.TargetNumber.ShouldBe(7);
        punch.Breakdown.ShouldBeSameAs(breakdown);
    }

    [Fact]
    public void Build_ForADeclarationRestriction_ReturnsADisabledRowWithTheReason()
    {
        // Arrange
        StubAttack(
            PhysicalAttackType.Punch,
            [Available(PartLocation.LeftArm)],
            PhysicalAttackRestriction.TargetAlreadyDisplaced);

        // Act
        var result = _sut.Build(_attacker, _target, _allUnits, (_, _) => { });

        // Assert
        var punch = Option(result, PhysicalAttackType.Punch);
        punch.IsEnabled.ShouldBeFalse();
        punch.RestrictionReason.ShouldBe("PhysicalAttackRestriction_TargetAlreadyDisplaced");
        punch.TargetNumber.ShouldBeNull();
        punch.Breakdown.ShouldBeNull();
        _calculator.DidNotReceive().GetToHitNumber(
            _attacker, _target, PhysicalAttackType.Punch, _map);
    }

    [Fact]
    public void Build_WhenNoLimbCanReach_ReturnsADisabledRowWithTheGenericReason()
    {
        // Arrange
        StubAttack(PhysicalAttackType.Punch, [], restriction: null);

        // Act
        var result = _sut.Build(_attacker, _target, _allUnits, (_, _) => { });

        // Assert
        var punch = Option(result, PhysicalAttackType.Punch);
        punch.IsEnabled.ShouldBeFalse();
        punch.RestrictionReason.ShouldBe("PhysicalAttackDeclaration_Unavailable");
        punch.LimbOptions.ShouldBeEmpty();
    }

    [Fact]
    public void Build_ForPush_ReturnsASingleBothArmsVariant()
    {
        // Arrange
        StubAttack(
            PhysicalAttackType.Push,
            [Available(PartLocation.LeftArm), Available(PartLocation.RightArm)],
            restriction: null);

        // Act
        var result = _sut.Build(_attacker, _target, _allUnits, (_, _) => { });

        // Assert
        var push = Option(result, PhysicalAttackType.Push);
        push.IsEnabled.ShouldBeTrue();
        var variant = push.LimbOptions.ShouldHaveSingleItem();
        variant.Label.ShouldBe("PhysicalAttack_BothArms");
        variant.Limbs.ShouldBe([PartLocation.LeftArm, PartLocation.RightArm]);
        variant.Damage.ShouldBe(0);
    }

    [Fact]
    public void Build_ForPunchWithTwoArms_AddsACombinedVariantWithTheSummedDamage()
    {
        // Arrange
        StubAttack(
            PhysicalAttackType.Punch,
            [Available(PartLocation.LeftArm), Available(PartLocation.RightArm)],
            restriction: null);
        _calculator.GetDamage(_attacker, PhysicalAttackType.Punch, PartLocation.LeftArm)
            .Returns(5);
        _calculator.GetDamage(_attacker, PhysicalAttackType.Punch, PartLocation.RightArm)
            .Returns(6);

        // Act
        var result = _sut.Build(_attacker, _target, _allUnits, (_, _) => { });

        // Assert
        var punch = Option(result, PhysicalAttackType.Punch);
        punch.LimbOptions.Count.ShouldBe(3);
        punch.LimbOptions.Single(option => option.Label == "MechPart_LeftArm")
            .Damage.ShouldBe(5);
        punch.LimbOptions.Single(option => option.Label == "MechPart_RightArm")
            .Damage.ShouldBe(6);

        var combined = punch.LimbOptions.Single(option => option.Label == "PhysicalAttack_BothArms");
        combined.Limbs.ShouldBe([PartLocation.LeftArm, PartLocation.RightArm]);
        combined.Damage.ShouldBe(11);
        combined.Variant.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public void Build_WhenOneLimbOfACombinedPunchIsRestricted_MarksTheCombinedVariantUnavailable()
    {
        // Arrange: the row still has a usable arm, but a two-arm punch needs both.
        StubAttack(
            PhysicalAttackType.Punch,
            [
                Available(PartLocation.LeftArm),
                new PhysicalAttackLimbEligibility(
                    PartLocation.RightArm,
                    PhysicalAttackRestriction.ArmDestroyed)
            ],
            restriction: null);

        // Act
        var result = _sut.Build(_attacker, _target, _allUnits, (_, _) => { });

        // Assert
        var punch = Option(result, PhysicalAttackType.Punch);
        punch.IsEnabled.ShouldBeTrue();
        var combined = punch.LimbOptions.Single(option => option.Label == "PhysicalAttack_BothArms");
        combined.Variant.Restriction.ShouldBe(PhysicalAttackRestriction.ArmDestroyed);
        combined.Variant.IsAvailable.ShouldBeFalse();
    }

    private void StubAttack(
        PhysicalAttackType attackType,
        IReadOnlyList<PhysicalAttackLimbEligibility> limbs,
        PhysicalAttackRestriction? restriction)
    {
        _eligibility.GetLimbEligibility(_attacker, _target, attackType, _map)
            .Returns(limbs);
        _eligibility.GetDeclarationRestriction(_attacker, _target, attackType, _map, _allUnits)
            .Returns(restriction);
    }

    private static PhysicalAttackOptionViewModel Option(
        IReadOnlyList<PhysicalAttackOptionViewModel> options,
        PhysicalAttackType attackType) =>
        options.Single(option => option.AttackType == attackType);

    private static PhysicalAttackLimbEligibility Available(PartLocation limb) =>
        new(limb, Restriction: null);

    private static PhysicalAttackBreakdown CreateBreakdown() => new()
    {
        PilotingBase = new PilotingRollModifier { Value = 5 },
        AttackTypeModifier = new PhysicalAttackBaseModifier
        {
            Value = 0,
            AttackType = PhysicalAttackType.Punch
        },
        AttackerMovement = new AttackerMovementModifier
        {
            Value = 0,
            MovementType = MovementType.StandingStill
        },
        TargetMovement = new TargetMovementModifier { Value = 0, HexesMoved = 0 },
        TerrainModifiers = [],
        OtherModifiers = []
    };
}
