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

public class PhysicalAttackOptionViewModelTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly List<(PhysicalAttackOptionViewModel Option, bool IsSelected)> _selections = [];

    public PhysicalAttackOptionViewModelTests()
    {
        _localizationService.GetString("Attack_TargetNumber").Returns("Target ToHit Number");
        _localizationService.GetString("Attack_ImpossibleToHit").Returns("Impossible to hit");
        _localizationService.GetString("PhysicalAttackType_Punch").Returns("Punch");
        _localizationService.GetString("PhysicalAttackType_Push").Returns("Push");
        _localizationService.GetString("PhysicalAttackDeclaration_Unavailable").Returns("Not available");
        _localizationService.GetString("Modifier_PilotingSkill").Returns("Piloting Skill: +{0}");
        _localizationService.GetString("Modifier_PhysicalAttackBase").Returns("{0} Base: {1:+#;-#;0}");
        _localizationService.GetString("Modifier_AttackerMovement").Returns("Attacker Movement ({0}): +{1}");
        _localizationService.GetString("Modifier_TargetMovement").Returns("Target Movement ({0} hexes): +{1}");
        _localizationService.GetString("Modifier_DamagedShoulder").Returns("Damaged Shoulder ({0}): +{1}");
    }

    private PhysicalAttackOptionViewModel CreateSut(
        PhysicalAttackType attackType = PhysicalAttackType.Punch,
        bool isEnabled = true,
        string? restrictionReason = null,
        int? targetNumber = 7,
        PhysicalAttackBreakdown? breakdown = null,
        IReadOnlyList<PhysicalAttackLimbVariant>? variants = null)
    {
        var limbVariants = variants ??
        [
            new PhysicalAttackLimbVariant("Punch (LA)", [PartLocation.LeftArm]),
            new PhysicalAttackLimbVariant("Punch (Both Arms)",
                [PartLocation.LeftArm, PartLocation.RightArm])
        ];
        return new PhysicalAttackOptionViewModel(
            attackType,
            limbVariants,
            limbVariants.FirstOrDefault(),
            isEnabled,
            restrictionReason,
            targetNumber,
            breakdown ?? CreateBreakdown(),
            (option, selected) => _selections.Add((option, selected)),
            _localizationService);
    }

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
    public void ExposesTheAttackTypeWithItsLocalizedName()
    {
        var sut = CreateSut();

        sut.AttackType.ShouldBe(PhysicalAttackType.Punch);
        sut.AttackTypeName.ShouldBe("Punch");
        sut.LimbVariants.Count.ShouldBe(2);
        sut.SelectedVariant.ShouldBe(sut.LimbVariants[0]);
    }

    [Fact]
    public void SelectingAnEnabledRowRunsTheCallback()
    {
        var sut = CreateSut();

        sut.IsSelected = true;

        sut.IsSelected.ShouldBeTrue();
        _selections.ShouldBe([(sut, true)]);
    }

    [Fact]
    public void DisabledRow_DoesNotChangeSelectionOrRunTheCallback()
    {
        var sut = CreateSut(isEnabled: false, restrictionReason: "No");

        sut.IsSelected = true;

        sut.IsSelected.ShouldBeFalse();
        _selections.ShouldBeEmpty();
    }

    [Fact]
    public void SettingTheValueToTheCurrentOne_DoesNotRunTheCallback()
    {
        var sut = CreateSut(isEnabled: true);
        sut.IsSelected = true;
        _selections.Clear();

        sut.IsSelected = true;

        _selections.ShouldBeEmpty();
    }

    [Fact]
    public void ChoosingAVariantOnAnUnselectedRowSelectsTheRow()
    {
        var sut = CreateSut();

        sut.SelectedVariant = sut.LimbVariants[1];

        sut.SelectedVariant.ShouldBe(sut.LimbVariants[1]);
        sut.IsSelected.ShouldBeTrue();
        _selections.ShouldBe([(sut, true)]);
    }

    [Fact]
    public void ChangingTheVariantOnASelectedRow_KeepsTheSelection()
    {
        var sut = CreateSut();
        sut.IsSelected = true;
        _selections.Clear();

        sut.SelectedVariant = sut.LimbVariants[1];

        sut.IsSelected.ShouldBeTrue();
        _selections.ShouldBeEmpty();
    }

    [Fact]
    public void DisabledRow_DoesNotChangeTheVariant()
    {
        var sut = CreateSut(isEnabled: false, restrictionReason: "No");
        var original = sut.SelectedVariant;

        sut.SelectedVariant = sut.LimbVariants[1];

        sut.SelectedVariant.ShouldBe(original);
    }

    [Fact]
    public void Description_ForAnUnavailableRow_IsTheRestrictionReason()
    {
        CreateSut(isEnabled: false, restrictionReason: "The target is too far")
            .AttackPossibilityDescription.ShouldBe("The target is too far");
    }

    [Fact]
    public void Description_ForAnUnavailableRowWithoutAReason_IsTheGenericFallback()
    {
        CreateSut(isEnabled: false).AttackPossibilityDescription.ShouldBe("Not available");
    }

    [Fact]
    public void Description_ForAnAvailableRow_RendersTheTargetNumberAndEveryModifier()
    {
        var description = CreateSut(targetNumber: 9).AttackPossibilityDescription;

        var lines = description.Split(Environment.NewLine);
        lines[0].ShouldBe("Target ToHit Number: 9");
        lines.ShouldContain("Piloting Skill: +5");
        lines.ShouldContain("Punch Base: 0");
        lines.ShouldContain("Target Movement (3 hexes): +1");
        lines.Length.ShouldBe(5);
    }

    [Fact]
    public void Description_ForAnImpossibleRow_AddsTheImpossibleText()
    {
        var description = CreateSut(targetNumber: 13).AttackPossibilityDescription;

        description.Split(Environment.NewLine).ShouldContain("Impossible to hit");
    }

    [Fact]
    public void Description_ForAPush_RendersTheDamagedShoulderModifier()
    {
        var breakdown = CreateBreakdown() with
        {
            OtherModifiers = [new DamagedShoulderModifier { Value = 4, DamagedShoulders = 2 }]
        };
        var sut = CreateSut(PhysicalAttackType.Push, breakdown: breakdown);

        sut.AttackPossibilityDescription.ShouldContain("Damaged Shoulder (2): +4");
    }

    [Fact]
    public void HitProbabilityText_IsDash_ForAnUnavailableRow()
    {
        CreateSut(isEnabled: false, restrictionReason: "No").HitProbabilityText.ShouldBe("-");
    }

    [Theory]
    [InlineData(13)]
    [InlineData(20)]
    public void HitProbabilityText_IsDash_ForAnImpossibleTargetNumber(int targetNumber)
    {
        var sut = CreateSut(targetNumber: targetNumber);

        sut.IsImpossible.ShouldBeTrue();
        sut.HitProbabilityText.ShouldBe("-");
    }

    [Fact]
    public void HitProbabilityText_ShowsTheProbability_ForAnAvailableRoll()
    {
        var sut = CreateSut(targetNumber: 7);

        sut.IsImpossible.ShouldBeFalse();
        sut.HitProbabilityText.ShouldBe("58%");
        sut.HitProbability.ShouldBeGreaterThan(0);
    }
}
