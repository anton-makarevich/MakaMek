using NSubstitute;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class AimedShotLocationSelectorViewModelTests
{
    private readonly Unit _target;
    private PartLocation? _selectedPart;
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    public AimedShotLocationSelectorViewModelTests()
    {
        var unitData = MechFactoryTests.CreateDummyMechData();
        _target = new MechFactory(new ClassicBattletechRulesProvider(), Substitute.For<ILocalizationService>())
            .Create(unitData);
    }


    [Fact]
    public void Constructor_ShouldInitializeAllBodyParts()
    {
        // Act
        var sut = CreateViewModel();

        // Assert
        sut.HeadPart.ShouldNotBeNull();
        sut.CenterTorsoPart.ShouldNotBeNull();
        sut.LeftTorsoPart.ShouldNotBeNull();
        sut.RightTorsoPart.ShouldNotBeNull();
        sut.LeftArmPart.ShouldNotBeNull();
        sut.RightArmPart.ShouldNotBeNull();
        sut.LeftLegPart.ShouldNotBeNull();
        sut.RightLegPart.ShouldNotBeNull();
    }

    [Fact]
    public void SelectPartCommand_ShouldCallOnPartSelectedCallback()
    {
        // Arrange
        var sut = CreateViewModel();

        // Act
        sut.SelectPart(PartLocation.Head);

        // Assert
        _selectedPart.ShouldBe(PartLocation.Head);
    }

    [Fact]
    public void UnitPartViewModel_WithDestroyedPart_ShouldNotBeSelectable()
    {
        // Arrange
        _target.Parts.First(p => p.Location == PartLocation.Head).ApplyDamage(1000, HitDirection.Front);
        var sut = CreateViewModel();

        // Act & Assert
        sut.HeadPart.IsDestroyed.ShouldBeTrue();
        sut.HeadPart.IsSelectable.ShouldBeFalse();
        sut.HeadPart.HitProbabilityText.ShouldBe("N/A");
    }

    [Fact]
    public void UnitPartViewModel_WithValidPart_ShouldCalculateHitProbability()
    {
        // Arrange
        var headModifiersBreakdown = CreateTestBreakdown(11);
        var otherModifiersBreakdown = CreateTestBreakdown(4);
        var sut = CreateViewModel(headModifiersBreakdown, otherModifiersBreakdown);

        // Act & Assert
        sut.HeadPart.HitProbability.ShouldBe(8.33d);
        sut.HeadPart.HitProbabilityText.ShouldBe("8%");
        sut.CenterTorsoPart.HitProbability.ShouldBe(91.67d);
        sut.CenterTorsoPart.HitProbabilityText.ShouldBe("92%");
    }

    [Fact]
    public void UnitPartViewModel_WithNoLineOfSight_ShouldHaveZeroHitProbability()
    {
        // Arrange
        var headModifiersBreakdown = CreateTestBreakdown(8, hasLineOfSight: false);
        var otherModifiersBreakdown = CreateTestBreakdown(5, hasLineOfSight: false);
        var sut = CreateViewModel(headModifiersBreakdown, otherModifiersBreakdown);

        // Act & Assert
        sut.HeadPart.HitProbability.ShouldBe(0);
        sut.HeadPart.HitProbabilityText.ShouldBe("0%");
        sut.CenterTorsoPart.HitProbability.ShouldBe(0);
        sut.CenterTorsoPart.HitProbabilityText.ShouldBe("0%");
    }

    [Fact]
    public void UnitPartViewModel_WithImpossibleTargetNumber_ShouldHaveZeroHitProbability()
    {
        // Arrange
        var headModifiersBreakdown = CreateTestBreakdown(13); // Impossible to hit
        var otherModifiersBreakdown = CreateTestBreakdown(15); // Impossible to hit
        var sut = CreateViewModel(headModifiersBreakdown, otherModifiersBreakdown);

        // Act & Assert
        sut.HeadPart.HitProbability.ShouldBe(0);
        sut.HeadPart.HitProbabilityText.ShouldBe("0%");
        sut.CenterTorsoPart.HitProbability.ShouldBe(0);
        sut.CenterTorsoPart.HitProbabilityText.ShouldBe("0%");
    }

    [Theory]
    [InlineData(PartLocation.Head)]
    [InlineData(PartLocation.CenterTorso)]
    [InlineData(PartLocation.LeftTorso)]
    [InlineData(PartLocation.RightTorso)]
    [InlineData(PartLocation.LeftArm)]
    [InlineData(PartLocation.RightArm)]
    [InlineData(PartLocation.LeftLeg)]
    [InlineData(PartLocation.RightLeg)]
    public void SelectPart_WithValidLocation_ShouldCallCallback(PartLocation location)
    {
        // Arrange
        var sut = CreateViewModel();
        _selectedPart.ShouldBeNull();
        
        // Act
        sut.SelectPart(location);

        // Assert
        _selectedPart.ShouldBe(location);
    }

    [Fact]
    public void UnitPartViewModel_ShouldHaveCorrectArmorAndStructureValues()
    {
        // Arrange
        var sut = CreateViewModel();

        // Act & Assert
        sut.HeadPart.MaxArmor.ShouldBeGreaterThan(0);
        sut.HeadPart.CurrentArmor.ShouldBe(sut.HeadPart.MaxArmor);
        sut.HeadPart.MaxStructure.ShouldBeGreaterThan(0);
        sut.HeadPart.CurrentStructure.ShouldBe(sut.HeadPart.MaxStructure);
    }

    [Fact]
    public void UnitPartViewModel_WithDamagedPart_ShouldReflectCurrentValues()
    {
        // Arrange
        var headPart = _target.Parts.First(p => p.Location == PartLocation.Head);
        headPart.ApplyDamage(5, HitDirection.Front); // Apply some damage
        var sut = CreateViewModel();

        // Act & Assert
        sut.HeadPart.CurrentArmor.ShouldBeLessThan(sut.HeadPart.MaxArmor);
        sut.HeadPart.IsSelectable.ShouldBeTrue(); // Still selectable if not destroyed
    }

    private AimedShotLocationSelectorViewModel CreateViewModel(
        ToHitBreakdown? aimedHeadModifiersBreakdown = null,
        ToHitBreakdown? aimedOtherModifiersBreakdown = null)
    {
        aimedHeadModifiersBreakdown ??= CreateTestBreakdown(5);
        aimedOtherModifiersBreakdown ??= CreateTestBreakdown(5);
        return new AimedShotLocationSelectorViewModel(
            _target,
            aimedHeadModifiersBreakdown,
            aimedOtherModifiersBreakdown,
            part => _selectedPart = part,
            _localizationService
        );
    }
    
    private ToHitBreakdown CreateTestBreakdown(int total, bool hasLineOfSight = true)
    {
        return new ToHitBreakdown
        {
            HasLineOfSight = hasLineOfSight,
            GunneryBase = new GunneryRollModifier { Value = total },
            AttackerMovement = new AttackerMovementModifier
            {
                MovementType = MovementType.StandingStill,
                Value = 0
            },
            TargetMovement = new TargetMovementModifier
            {
                HexesMoved = 0,
                Value = 0
            },
            OtherModifiers = [],
            RangeModifier = new RangeRollModifier
            {
                Value = 0,
                Range = WeaponRange.Long,
                Distance = 5,
                WeaponName = "Test"
            },
            TerrainModifiers = []
        };
    }
}
