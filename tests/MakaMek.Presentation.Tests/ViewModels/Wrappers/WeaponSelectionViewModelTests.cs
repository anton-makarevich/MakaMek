using NSubstitute;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class WeaponSelectionViewModelTests
{
    private readonly Weapon _weapon;
    private readonly Mech _target;
    private Action<Weapon, bool>? _selectionChangedAction;
    private readonly Action<AimedShotLocationSelectorViewModel> _onShowAimedShotLocationSelector = Substitute.For<Action<AimedShotLocationSelectorViewModel>>();
    private readonly Action _onHideAimedShotLocationSelector = Substitute.For<Action>();
    private WeaponSelectionViewModel _sut = null!;
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    public WeaponSelectionViewModelTests()
    {
        _weapon = new MediumLaser();
        var part = new Arm("Left Arm", PartLocation.LeftArm, 1, 1);
        part.TryAddComponent(_weapon);
        
        // Create a test mech using MechFactory
        var structureValueProvider = Substitute.For<IRulesProvider>();
        structureValueProvider.GetStructureValues(20).Returns(new Dictionary<PartLocation, int>
        {
            { PartLocation.Head, 8 },
            { PartLocation.CenterTorso, 10 },
            { PartLocation.LeftTorso, 8 },
            { PartLocation.RightTorso, 8 },
            { PartLocation.LeftArm, 4 },
            { PartLocation.RightArm, 4 },
            { PartLocation.LeftLeg, 8 },
            { PartLocation.RightLeg, 8 }
        });
        var mechFactory = new MechFactory(structureValueProvider, Substitute.For<ILocalizationService>());
        var mechData = MechFactoryTests.CreateDummyMechData();
        _target = mechFactory.Create(mechData);
        
        _localizationService.GetString("MechPart_Head_Short").Returns("H");
        _localizationService.GetString("MechPart_LeftArm_Short").Returns("LA");
        _localizationService.GetString("MechPart_RightLeg_Short").Returns("RL");
    }

    [Fact]
    public void Constructor_InitializesPropertiesCorrectly()
    {
        // Arrange
        const bool isInRange = true;
        const bool isSelected = false;
        const bool isEnabled = true;

        // Act
        CreateSut(isInRange, isSelected, isEnabled, _target);

        // Assert
        _sut.Weapon.ShouldBe(_weapon);
        _sut.IsInRange.ShouldBe(isInRange);
        _sut.IsSelected.ShouldBe(isSelected);
        _sut.IsEnabled.ShouldBeFalse(); //default HitProbability is zero
        _sut.Target.ShouldBe(_target);
        _sut.ModifiersBreakdown = CreateTestBreakdown(5);
        _sut.IsEnabled.ShouldBe(isEnabled);
    }

    [Fact]
    public void Name_ReturnsWeaponName()
    {
        // Arrange
        CreateSut();

        // Act & Assert
        _sut.Name.ShouldBe("Medium Laser");
    }

    [Fact]
    public void RangeInfo_ReturnsCorrectFormat()
    {
        // Arrange
        CreateSut();

        // Act & Assert
        _sut.RangeInfo.ShouldBe("9");
    }

    [Fact]
    public void Damage_ReturnsCorrectFormat()
    {
        // Arrange
        CreateSut();

        // Act & Assert
        _sut.Damage.ShouldBe("5");
    }

    [Fact]
    public void Heat_ReturnsCorrectFormat()
    {
        // Arrange
        CreateSut();

        // Act & Assert
        _sut.Heat.ShouldBe("3");
    }

    [Fact]
    public void Ammo_ReturnsEmptyString_WhenWeaponDoesNotRequireAmmo()
    {
        // Arrange
        CreateSut();

        // Act & Assert
        _sut.Ammo.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(0)]
    public void Ammo_ReturnsFormattedString_WhenWeaponRequiresAmmo(int remainingShots)
    {
        // Arrange
        var ballisticWeapon = new TestBallisticWeapon();
        _sut = new WeaponSelectionViewModel(
            ballisticWeapon,
            true,
            false,
            true,
            null,
            (w, s) => _selectionChangedAction?.Invoke(w, s),
            _onShowAimedShotLocationSelector,
            _onHideAimedShotLocationSelector,
            _localizationService,
            remainingShots);

        // Act & Assert
        _sut.Ammo.ShouldBe(remainingShots.ToString());
    }

    [Fact]
    public void RequiresAmmo_ReturnsFalse_ForEnergyWeapons()
    {
        // Arrange
        CreateSut();

        // Act & Assert
        _sut.RequiresAmmo.ShouldBeFalse();
    }

    [Fact]
    public void RequiresAmmo_ReturnsTrue_ForBallisticWeapons()
    {
        // Arrange
        var ballisticWeapon = new TestBallisticWeapon();
        _sut = new WeaponSelectionViewModel(
            ballisticWeapon,
            true,
            false,
            true,
            null,
            (w, s) => _selectionChangedAction?.Invoke(w, s),
            _onShowAimedShotLocationSelector,
            _onHideAimedShotLocationSelector,
            _localizationService);

        // Act & Assert
        _sut.RequiresAmmo.ShouldBeTrue();
    }

    [Fact]
    public void HasSufficientAmmo_ReturnsTrue_WhenWeaponDoesNotRequireAmmo()
    {
        // Arrange
        CreateSut();

        // Act & Assert
        _sut.HasSufficientAmmo.ShouldBeTrue();
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void HasSufficientAmmo_ReturnsExpectedValue_BasedOnRemainingShots(int remainingShots, bool expected)
    {
        // Arrange
        var ballisticWeapon = new TestBallisticWeapon();
        _sut = new WeaponSelectionViewModel(
            ballisticWeapon,
            true,
            false,
            true,
            null,
            (w, s) => _selectionChangedAction?.Invoke(w, s),
            _onShowAimedShotLocationSelector,
            _onHideAimedShotLocationSelector,
            _localizationService,
            remainingShots);

        // Act & Assert
        _sut.HasSufficientAmmo.ShouldBe(expected);
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenNoAmmoAvailable()
    {
        // Arrange
        var ballisticWeapon = new TestBallisticWeapon();
        var part = new Arm("Left Arm", PartLocation.LeftArm, 1, 1);
        part.TryAddComponent(ballisticWeapon);
        _sut = new WeaponSelectionViewModel(
            ballisticWeapon,
            true,
            false,
            true,
            null,
            (w, s) => _selectionChangedAction?.Invoke(w, s),
            _onShowAimedShotLocationSelector,
            _onHideAimedShotLocationSelector,
            _localizationService,
            0)
        {
            // Set a valid hit probability
            ModifiersBreakdown = CreateTestBreakdown(5)
        };

        // Act & Assert
        _sut.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenWeaponIsNotAvailable()
    {
        // Arrange
        var ballisticWeapon = new TestBallisticWeapon();
        var part = new Arm("Left Arm", PartLocation.LeftArm, 1, 1);
        part.TryAddComponent(ballisticWeapon);
        ballisticWeapon.Hit();
        _sut = new WeaponSelectionViewModel(
            ballisticWeapon,
            true,
            false,
            true,
            null,
            (w, s) => _selectionChangedAction?.Invoke(w, s),
            _onShowAimedShotLocationSelector,
            _onHideAimedShotLocationSelector,
            _localizationService,
            5)
        {
            // Set a valid hit probability
            ModifiersBreakdown = CreateTestBreakdown(5)
        };

        // Act & Assert
        _sut.IsEnabled.ShouldBeFalse();
    }
    
    [Fact]
    public void AttackPossibilityDescription_ReturnsNoAmmoMessage_WhenWeaponRequiresAmmoButHasNone()
    {
        // Arrange
        var ballisticWeapon = new TestBallisticWeapon();
        _localizationService.GetString("Attack_NoAmmo").Returns("No ammunition");
        
        _sut = new WeaponSelectionViewModel(
            ballisticWeapon,
            true,
            false,
            true,
            null,
            (w, s) => _selectionChangedAction?.Invoke(w, s),
            _onShowAimedShotLocationSelector,
            _onHideAimedShotLocationSelector,
            _localizationService,
            0);

        // Act
        var result = _sut.AttackPossibilityDescription;

        // Assert
        result.ShouldBe("No ammunition");
        _localizationService.Received().GetString("Attack_NoAmmo");
    }

    [Fact]
    public void IsSelected_WhenDisabled_CannotBeSetToTrue()
    {
        // Arrange
        CreateSut(isEnabled: false);

        // Act
        _sut.IsSelected = true;

        // Assert
        _sut.IsSelected.ShouldBeFalse();
        _selectionChangedAction.ShouldBeNull();
    }

    [Fact]
     public void IsSelected_WhenEnabled_CanBeChanged()
     {
         // Arrange
         CreateSut(isEnabled: true);
         var wasActionCalled = false;
         var expectedValue = true;
         _selectionChangedAction = (weapon, selected) =>
         {
             weapon.ShouldBe(_weapon);
             selected.ShouldBe(expectedValue);
             wasActionCalled = true;
         };
         _sut.ModifiersBreakdown = CreateTestBreakdown(5);
    
         // Act
         _sut.IsSelected = true;
    
         // Assert
         _sut.IsSelected.ShouldBeTrue();
         wasActionCalled.ShouldBeTrue();
    
         // Test deselection
         expectedValue = false;
         wasActionCalled = false;
         _sut.IsSelected = false;
    
         _sut.IsSelected.ShouldBeFalse();
         wasActionCalled.ShouldBeTrue();
     }

    [Fact]
    public void ModifiersBreakdown_CanBeSetAndRetrieved()
    {
        // Arrange
        const bool isInRange = true;
        const bool isSelected = false;
        const bool isEnabled = true;
        var testBreakdown = CreateTestBreakdown(8);
        
        _selectionChangedAction = Substitute.For<Action<Weapon, bool>>();
        _sut = new WeaponSelectionViewModel(
            _weapon,
            isInRange,
            isSelected,
            isEnabled,
            _target,
            _selectionChangedAction,
            _onShowAimedShotLocationSelector,
            _onHideAimedShotLocationSelector,
            _localizationService
            )
        {
            // Act
            ModifiersBreakdown = testBreakdown
        };

        // Assert
        _sut.ModifiersBreakdown.ShouldBe(testBreakdown);
    }
    
    [Fact]
    public void ModifiersBreakdown_NotifiesPropertyChanged()
    {
        // Arrange
        const bool isInRange = true;
        const bool isSelected = false;
        const bool isEnabled = true;
        var initialBreakdown = CreateTestBreakdown(8);
        var updatedBreakdown = CreateTestBreakdown(5);
        
        _selectionChangedAction = Substitute.For<Action<Weapon, bool>>();
        _sut = new WeaponSelectionViewModel(
            _weapon,
            isInRange,
            isSelected,
            isEnabled,
            _target,
            _selectionChangedAction,
            _onShowAimedShotLocationSelector,
            _onHideAimedShotLocationSelector,
            _localizationService)
        {
            ModifiersBreakdown = initialBreakdown
        };

        var propertyChangedRaised = false;
        _sut.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(WeaponSelectionViewModel.HitProbability))
                propertyChangedRaised = true;
        };
        
        // Act
        _sut.ModifiersBreakdown = updatedBreakdown;
        
        // Assert
        propertyChangedRaised.ShouldBeTrue();
        _sut.ModifiersBreakdown.ShouldBe(updatedBreakdown);
    }
    
    [Fact]
    public void HitProbabilityText_FormatsCorrectly()
    {
        // Arrange
        CreateSut();
        
        // Act & Assert - Positive probability
        _sut.ModifiersBreakdown = CreateTestBreakdown(6);
        _sut.HitProbabilityText.ShouldBe("72%");
        
        // Act & Assert - Zero probability
        _sut.ModifiersBreakdown = CreateTestBreakdown(13,false);
        _sut.HitProbabilityText.ShouldBe("-");
    }
    
    [Fact]
    public void IsEnabled_ReturnsFalseWhenHitProbabilityIsZero()
    {
        // Arrange
        CreateSut(isEnabled: true);
        
        // Act - Set hit probability to zero
        _sut.ModifiersBreakdown = CreateTestBreakdown(13,false);
        
        // Assert - Should be disabled despite isEnabled being true
        _sut.IsEnabled.ShouldBeFalse();
        
        // Act - Set hit probability to positive value
        _sut.ModifiersBreakdown = CreateTestBreakdown(6);
        
        // Assert - Should be enabled
        _sut.IsEnabled.ShouldBeTrue();
    }
    
    [Fact]
    public void IsEnabled_ReturnsFalseWhenExplicitlyDisabled()
    {
        // Arrange
        CreateSut(isEnabled: false);
        
        // Act - Set hit probability to positive value
        _sut.ModifiersBreakdown = CreateTestBreakdown(6);
        
        // Assert - Should still be disabled because isEnabled is false
        _sut.IsEnabled.ShouldBeFalse();
    }
    
    [Fact]
    public void AttackPossibilityDescription_FormatsCorrectly()
    {
        // Arrange
        CreateSut();
        var breakdown = CreateTestBreakdown(8);
        _localizationService.GetString("Attack_TargetNumber").Returns("Target Number");
        
        // Act
        _sut.ModifiersBreakdown = breakdown;
        
        // Assert
        var description = _sut.AttackPossibilityDescription;
        description.ShouldContain("Target Number: 8");
        
        // The Format method of each modifier should be called
        _localizationService.Received().GetString("Attack_TargetNumber");
        
        // Each modifier's Format method would be called, but we can't verify that directly
        // in this test since we're using real objects, not mocks
    }
    
    [Fact]
    public void AttackPossibilityDescription_HandlesNoLineOfSight()
    {
        // Arrange
        CreateSut();
        var breakdown = CreateTestBreakdown(8, hasLineOfSight: false);
        _localizationService.GetString("Attack_NoLineOfSight").Returns("No Line Of Sight");
        
        // Act
        _sut.ModifiersBreakdown = breakdown;
        
        // Assert
        _sut.AttackPossibilityDescription.ShouldBe("No Line Of Sight");
        _localizationService.Received().GetString("Attack_NoLineOfSight");
    }
    
    [Fact]
    public void AttackPossibilityDescription_HandlesNullBreakdown()
    {
        // Arrange
        CreateSut();
        _localizationService.GetString("Attack_NoModifiersCalculated").Returns("No modifiers calculated");
        
        // Act
        _sut.ModifiersBreakdown = null;
        
        // Assert
        _sut.AttackPossibilityDescription.ShouldBe("No modifiers calculated");
        _localizationService.Received().GetString("Attack_NoModifiersCalculated");
    }
    
    [Fact]
    public void AttackPossibilityDescription_HandlesOutOfRange()
    {
        // Arrange
        CreateSut(isInRange: false);
        _localizationService.GetString("Attack_OutOfRange").Returns("Target out of range");
        
        // Act & Assert
        _sut.AttackPossibilityDescription.ShouldBe("Target out of range");
        _localizationService.Received().GetString("Attack_OutOfRange");
    }
    
    [Fact]
    public void AttackPossibilityDescription_HandlesTargetingDifferentTarget()
    {
        // Arrange
        CreateSut(isEnabled: false, target: _target);
        _localizationService.GetString("Attack_Targeting").Returns("Already targeting {0}");
        
        // Act & Assert
        _sut.AttackPossibilityDescription.ShouldBe("Already targeting Locust LCT-1V");
        _localizationService.Received().GetString("Attack_Targeting");
    }
    
    [Fact]
    public void HitProbability_ReturnsZeroWhenDisabled()
    {
        // Arrange
        CreateSut(isEnabled: false);
        
        // Set a valid breakdown that would normally give a positive hit probability
        _sut.ModifiersBreakdown = CreateTestBreakdown(6);
        
        // Act & Assert
        _sut.HitProbability.ShouldBe(0);
    }

    [Fact]
    public void AttackPossibilityDescription_HandlesWeaponDestroyed()
    {
        // Arrange
        CreateSut();
        _weapon.Hit();
        _localizationService.GetString("Attack_WeaponDestroyed").Returns("Weapon is destroyed");

        // Act
        var result = _sut.AttackPossibilityDescription;

        // Assert
        result.ShouldBe("Weapon is destroyed");
        _localizationService.Received().GetString("Attack_WeaponDestroyed");
    }

    [Fact]
    public void AttackPossibilityDescription_HandlesLocationDestroyed()
    {
        // Arrange
        CreateSut();
        var part = _weapon.MountedOn;
        if (part == null) throw new Exception("Weapon must be mounted on a part for this test.");
        part.ApplyDamage(1000);
        _localizationService.GetString("Attack_LocationDestroyed").Returns("Location is destroyed");

        // Act
        var result = _sut.AttackPossibilityDescription;

        // Assert
        result.ShouldBe("Location is destroyed");
        _localizationService.Received().GetString("Attack_LocationDestroyed");
    }
    
    [Fact]
    public void AimedShotTarget_WhenSet_ShouldUpdateIsAimedShot()
    {
        // Arrange
        CreateSut();

        // Act
        _sut.AimedShotTarget = PartLocation.Head;

        // Assert
        _sut.IsAimedShot.ShouldBeTrue();
        _sut.AimedShotTarget.ShouldBe(PartLocation.Head);
    }

    [Fact]
    public void AimedShotTarget_WhenNull_ShouldNotBeAimedShot()
    {
        // Arrange
        CreateSut();

        // Act
        _sut.AimedShotTarget = null;

        // Assert
        _sut.IsAimedShot.ShouldBeFalse();
        _sut.AimedShotTarget.ShouldBeNull();
    }

    [Fact]
    public void AimedShotText_WithoutAimedShot_ShouldBeEmpty()
    {
        // Arrange
        CreateSut();

        // Act & Assert
        _sut.AimedShotText.ShouldBe(string.Empty);
    }

    [Fact]
    public void ClearAimedShot_ShouldResetAimedShotTarget()
    {
        // Arrange
        CreateSut();
        _sut.AimedShotTarget = PartLocation.Head;

        // Act
        _sut.ClearAimedShot();

        // Assert
        _sut.AimedShotTarget.ShouldBeNull();
        _sut.IsAimedShot.ShouldBeFalse();
    }

    [Theory]
    [InlineData(PartLocation.Head, "H")]
    [InlineData(PartLocation.LeftArm, "LA")]
    [InlineData(PartLocation.RightLeg, "RL")]
    public void AimedShotText_WithDifferentLocations_ShouldFormatCorrectly(PartLocation location, string expected)
    {
        // Arrange
        CreateSut();

        // Act
        _sut.AimedShotTarget = location;

        // Assert
        _sut.AimedShotText.ShouldBe(expected);
    }

    private void CreateSut(
        bool isInRange = true,
        bool isSelected = false,
        bool isEnabled = true,
        Unit? target = null)
    {
        _sut = new WeaponSelectionViewModel(
            _weapon,
            isInRange,
            isSelected,
            isEnabled,
            target,
            (w, s) => _selectionChangedAction?.Invoke(w, s),
            _onShowAimedShotLocationSelector,
            _onHideAimedShotLocationSelector,
            _localizationService);
    }

    private ToHitBreakdown CreateTestBreakdown(int total, bool hasLineOfSight = true)
    {
        // Create a breakdown that will result in the specified total
        var gunneryValue = 4;
        var otherModifiers = total - gunneryValue;

        return new ToHitBreakdown
        {
            GunneryBase = new GunneryRollModifier { Value = gunneryValue },
            AttackerMovement = new AttackerMovementModifier { Value = 0, MovementType = MovementType.StandingStill },

            TargetMovement = new TargetMovementModifier { Value = 0, HexesMoved = 0 },
            RangeModifier = new RangeRollModifier
                { Value = otherModifiers, Range = WeaponRange.Medium, Distance = 5, WeaponName = "Test" },
            OtherModifiers = [],
            TerrainModifiers = [],
            HasLineOfSight = hasLineOfSight
        };
    }

    private class TestBallisticWeapon()
        : Weapon(new WeaponDefinition(
            "AC/5", 5, 1, 
            0, 3, 6, 9, 
            WeaponType.Ballistic, 10, 1, 1, 1,1,MakaMekComponent.AC5, MakaMekComponent.ISAmmoAC5))
    {
    }
}
