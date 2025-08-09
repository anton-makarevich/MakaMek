using NSubstitute;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;
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
    public void BodyPartViewModel_WithDestroyedPart_ShouldNotBeSelectable()
    {
        // Arrange
        _target.Parts.First(p => p.Location == PartLocation.Head).ApplyDamage(1000);
        var sut = CreateViewModel();

        // Act & Assert
        sut.HeadPart.IsDestroyed.ShouldBeTrue();
        sut.HeadPart.IsSelectable.ShouldBeFalse();
        sut.HeadPart.HitProbabilityText.ShouldBe("N/A");
    }

    [Fact]
    public void BodyPartViewModel_WithValidPart_ShouldCalculateHitProbability()
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
    
    private ToHitBreakdown CreateTestBreakdown(int total)
    {
        return new ToHitBreakdown
        {
            HasLineOfSight = true,
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
