using NSubstitute;
using Sanet.Localization;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class PhysicalAttackPreviewViewModelTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IUnit _attacker = Substitute.For<IUnit>();
    private readonly IUnit _target = Substitute.For<IUnit>();

    public PhysicalAttackPreviewViewModelTests()
    {
        _attacker.Name.Returns("Attacker");
        _target.Name.Returns("Target");

        _localizationService.GetString("Attack_TargetNumber").Returns("Target ToHit Number");
        _localizationService.GetString("Attack_ImpossibleToHit").Returns("Impossible to hit");
        _localizationService.GetString("PhysicalAttackType_Punch").Returns("Punch");
        _localizationService.GetString("Modifier_PilotingSkill").Returns("Piloting Skill: +{0}");
        _localizationService.GetString("Modifier_PhysicalAttackBase").Returns("{0} Base: {1:+#;-#;0}");
        _localizationService.GetString("Modifier_AttackerMovement").Returns("Attacker Movement ({0}): +{1}");
        _localizationService.GetString("Modifier_TargetMovement").Returns("Target Movement ({0} hexes): +{1}");
    }

    private PhysicalAttackPreviewViewModel CreateSut(int targetNumber = 7) => new(
        _attacker,
        _target,
        PhysicalAttackType.Punch,
        targetNumber,
        CreateBreakdown(),
        _localizationService);

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
        TargetMovement = new TargetMovementModifier { Value = 1, HexesMoved = 3 },
        TerrainModifiers = [],
        OtherModifiers = []
    };

    [Fact]
    public void ExposesTheAttackTypeAndTarget()
    {
        var sut = CreateSut();

        sut.AttackType.ShouldBe(PhysicalAttackType.Punch);
        sut.AttackTypeName.ShouldBe("Punch");
        sut.Attacker.ShouldBe(_attacker);
        sut.Target.ShouldBe(_target);
        sut.TargetName.ShouldBe("Target");
    }

    [Fact]
    public void FormatsTheTargetNumber()
    {
        CreateSut(9).TargetNumberText.ShouldBe("Target ToHit Number: 9");
    }

    [Fact]
    public void RendersEveryModifierLine()
    {
        var lines = CreateSut().ModifierLines;

        lines.Count.ShouldBe(4);
        lines.ShouldContain("Piloting Skill: +5");
        lines.ShouldContain("Punch Base: 0");
        lines.ShouldContain("Target Movement (3 hexes): +1");
    }

    [Theory]
    [InlineData(12, false)]
    [InlineData(13, true)]
    public void ReportsWhetherTheAttackIsImpossible(int targetNumber, bool expected)
    {
        CreateSut(targetNumber).IsImpossible.ShouldBe(expected);
    }

    [Fact]
    public void ImpossibleAttacksExposeTheLocalizedReason()
    {
        CreateSut(13).ImpossibleText.ShouldBe("Impossible to hit");
    }
}
