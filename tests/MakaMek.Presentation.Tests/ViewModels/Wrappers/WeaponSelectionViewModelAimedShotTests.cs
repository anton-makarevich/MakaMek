using NSubstitute;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class WeaponSelectionViewModelAimedShotTests
{
    private readonly Weapon _weapon;
    private readonly Unit _target = Substitute.For<Unit>();
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly Action<Weapon, bool> _onSelectionChanged = Substitute.For<Action<Weapon, bool>>();
    private readonly Action<AimedShotLocationSelectorViewModel> _onShowSelector = Substitute.For<Action<AimedShotLocationSelectorViewModel>>();
    private readonly Action _onHideSelector = Substitute.For<Action>();

    public WeaponSelectionViewModelAimedShotTests()
    {
        _weapon = Substitute.For<Weapon>();
        
        _weapon.Name.Returns("Test Weapon");
        _weapon.IsAvailable.Returns(true);
        _weapon.RequiresAmmo.Returns(false);
    }

    [Fact]
    public void AimedShotTarget_WhenSet_ShouldUpdateIsAimedShot()
    {
        // Arrange
        var sut = CreateViewModel();

        // Act
        sut.AimedShotTarget = PartLocation.Head;

        // Assert
        sut.IsAimedShot.ShouldBeTrue();
        sut.AimedShotTarget.ShouldBe(PartLocation.Head);
    }

    [Fact]
    public void AimedShotTarget_WhenNull_ShouldNotBeAimedShot()
    {
        // Arrange
        var sut = CreateViewModel();

        // Act
        sut.AimedShotTarget = null;

        // Assert
        sut.IsAimedShot.ShouldBeFalse();
        sut.AimedShotTarget.ShouldBeNull();
    }

    [Fact]
    public void AimedShotText_WithAimedShot_ShouldShowTargetLocation()
    {
        // Arrange
        var sut = CreateViewModel();

        // Act
        sut.AimedShotTarget = PartLocation.CenterTorso;

        // Assert
        sut.AimedShotText.ShouldBe("Aimed: CenterTorso");
    }

    [Fact]
    public void AimedShotText_WithoutAimedShot_ShouldBeEmpty()
    {
        // Arrange
        var sut = CreateViewModel();

        // Act & Assert
        sut.AimedShotText.ShouldBe(string.Empty);
    }

    [Fact]
    public void ClearAimedShot_ShouldResetAimedShotTarget()
    {
        // Arrange
        var sut = CreateViewModel();
        sut.AimedShotTarget = PartLocation.Head;

        // Act
        sut.ClearAimedShot();

        // Assert
        sut.AimedShotTarget.ShouldBeNull();
        sut.IsAimedShot.ShouldBeFalse();
    }

    [Theory]
    [InlineData(PartLocation.Head, "Aimed: Head")]
    [InlineData(PartLocation.LeftArm, "Aimed: LeftArm")]
    [InlineData(PartLocation.RightLeg, "Aimed: RightLeg")]
    public void AimedShotText_WithDifferentLocations_ShouldFormatCorrectly(PartLocation location, string expected)
    {
        // Arrange
        var sut = CreateViewModel();

        // Act
        sut.AimedShotTarget = location;

        // Assert
        sut.AimedShotText.ShouldBe(expected);
    }

    private WeaponSelectionViewModel CreateViewModel()
    {
        return new WeaponSelectionViewModel(
            _weapon,
            isInRange: true,
            isSelected: false,
            isEnabled: true,
            _target,
            _onSelectionChanged,
            _onShowSelector,
            _onHideSelector,
            _localizationService,
            remainingAmmoShots: 10);
    }
}
