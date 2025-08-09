using NSubstitute;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
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
        var sut = CreateViewModel();

        // Act & Assert
        // TODO a placeholder
        true.ShouldBeTrue();
    }

    private AimedShotLocationSelectorViewModel CreateViewModel()
    {
        return new AimedShotLocationSelectorViewModel(
            _target,
            Substitute.For<ToHitBreakdown>(),
            Substitute.For<ToHitBreakdown>(),
            part => _selectedPart = part);
    }
}
