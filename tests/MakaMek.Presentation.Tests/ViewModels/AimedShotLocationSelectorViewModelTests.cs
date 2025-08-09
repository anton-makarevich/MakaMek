using NSubstitute;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class AimedShotLocationSelectorViewModelTests
{
    private readonly Unit _target = Substitute.For<Unit>();
    private PartLocation? _selectedPart;

    [Fact]
    public void Constructor_ShouldInitializeAllBodyParts()
    {
        // Arrange
        SetupMockTarget();

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
        SetupMockTarget();
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
        SetupMockTarget(headDestroyed: true);
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
        SetupMockTarget();
        var sut = CreateViewModel();

        // Act & Assert
        sut.HeadPart.IsSelectable.ShouldBeTrue();
        sut.HeadPart.HitProbability.ShouldBe(75.0);
        sut.HeadPart.HitProbabilityText.ShouldBe("75%");
    }

    private AimedShotLocationSelectorViewModel CreateViewModel()
    {
        return new AimedShotLocationSelectorViewModel(
            _target,
            Substitute.For<ToHitBreakdown>(),
            Substitute.For<ToHitBreakdown>(),
            part => _selectedPart = part);
    }

    private void SetupMockTarget(bool headDestroyed = false)
    {
        var headPart = Substitute.For<UnitPart>();
        headPart.Location.Returns(PartLocation.Head);
        headPart.IsDestroyed.Returns(headDestroyed);

        var centerTorsoPart = Substitute.For<UnitPart>();
        centerTorsoPart.Location.Returns(PartLocation.CenterTorso);
        centerTorsoPart.IsDestroyed.Returns(false);

        var leftTorsoPart = Substitute.For<UnitPart>();
        leftTorsoPart.Location.Returns(PartLocation.LeftTorso);
        leftTorsoPart.IsDestroyed.Returns(false);

        var rightTorsoPart = Substitute.For<UnitPart>();
        rightTorsoPart.Location.Returns(PartLocation.RightTorso);
        rightTorsoPart.IsDestroyed.Returns(false);

        var leftArmPart = Substitute.For<UnitPart>();
        leftArmPart.Location.Returns(PartLocation.LeftArm);
        leftArmPart.IsDestroyed.Returns(false);

        var rightArmPart = Substitute.For<UnitPart>();
        rightArmPart.Location.Returns(PartLocation.RightArm);
        rightArmPart.IsDestroyed.Returns(false);

        var leftLegPart = Substitute.For<UnitPart>();
        leftLegPart.Location.Returns(PartLocation.LeftLeg);
        leftLegPart.IsDestroyed.Returns(false);

        var rightLegPart = Substitute.For<UnitPart>();
        rightLegPart.Location.Returns(PartLocation.RightLeg);
        rightLegPart.IsDestroyed.Returns(false);

        _target.Parts.Returns([
            headPart, centerTorsoPart, leftTorsoPart, rightTorsoPart,
            leftArmPart, rightArmPart, leftLegPart, rightLegPart
        ]);
    }
}
