using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units;

public class UnitWeaponAttackStateTests
{
    private readonly UnitWeaponAttackState _sut;
    private readonly Mech _attacker;
    private readonly Mech _target1;
    private readonly Mech _target2;
    private readonly Weapon _leftArmWeapon;
    private readonly Weapon _torsoWeapon;
    private readonly MechFactory _mechFactory;

    public UnitWeaponAttackStateTests()
    {
        _sut = new UnitWeaponAttackState();
        
        var localizationService = Substitute.For<ILocalizationService>();
        _mechFactory = new MechFactory(
            new ClassicBattletechRulesProvider(),
            new ClassicBattletechComponentProvider(),
            localizationService);
        
        // Create mock units
        var data = MechFactoryTests.CreateDummyMechData();
        _attacker = _mechFactory.Create(data);
        _target1 = _mechFactory.Create(data);
        _target2 = _mechFactory.Create(data);
        
        // Create mock weapons with different locations
        _leftArmWeapon = CreateWeapon(PartLocation.LeftArm, _attacker);
        
        _torsoWeapon = CreateWeapon(PartLocation.CenterTorso, _attacker);
        
        // Setup attacker position for primary target calculation
        _attacker.Deploy(new HexPosition(new HexCoordinates(3, 3), HexDirection.Bottom));
        _target1.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        _target2.Deploy(new HexPosition(new HexCoordinates(3, 4), HexDirection.Bottom));
    }

    private Weapon CreateWeapon(PartLocation location, Mech mech)
    {
        var part = mech.Parts[location];
        var weapon = new MediumLaser();
        part.TryAddComponent(weapon).ShouldBeTrue();
        
        return weapon;
    }

    [Fact]
    public void SetWeaponTarget_ShouldAddWeaponToTargets()
    {
        // Act
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);

        // Assert
        _sut.WeaponTargets.ShouldContainKeyAndValue(_leftArmWeapon, _target1);
        _sut.SelectedWeapons.ShouldContain(_leftArmWeapon);
        _sut.AllTargets.ShouldContain(_target1);
    }

    [Fact]
    public void SetWeaponTarget_WithProneMech_ShouldSetCommittedArm()
    {
        // Arrange
        _attacker.SetProne();

        // Act
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);

        // Assert
        _sut.CommittedArmLocation.ShouldBe(PartLocation.LeftArm);
    }

    [Fact]
    public void SetWeaponTarget_WithNonProneMech_ShouldNotSetCommittedArm()
    {
        // Act
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);

        // Assert
        _sut.CommittedArmLocation.ShouldBeNull();
    }

    [Fact]
    public void SetWeaponTarget_WithSingleTarget_ShouldSetPrimaryTarget()
    {
        // Act
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);

        // Assert
        _sut.PrimaryTarget.ShouldBe(_target1);
    }

    [Fact]
    public void RemoveWeaponTarget_ShouldRemoveWeaponFromTargets()
    {
        // Arrange
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);

        // Act
        _sut.RemoveWeaponTarget(_leftArmWeapon, _attacker);

        // Assert
        _sut.WeaponTargets.ShouldNotContainKey(_leftArmWeapon);
        _sut.SelectedWeapons.ShouldNotContain(_leftArmWeapon);
    }

    [Fact]
    public void RemoveWeaponTarget_WithProneMech_ShouldClearCommittedArmWhenNoArmWeaponsLeft()
    {
        // Arrange
        _attacker.SetProne();
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);

        // Act
        _sut.RemoveWeaponTarget(_leftArmWeapon, _attacker);

        // Assert
        _sut.CommittedArmLocation.ShouldBeNull();
    }

    [Fact]
    public void ClearAllWeaponTargets_ShouldClearAllState()
    {
        // Arrange
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);
        _sut.SetWeaponTarget(_torsoWeapon, _target2, _attacker);

        // Act
        _sut.ClearAllWeaponTargets();

        // Assert
        _sut.WeaponTargets.ShouldBeEmpty();
        _sut.SelectedWeapons.ShouldBeEmpty();
        _sut.AllTargets.ShouldBeEmpty();
        _sut.PrimaryTarget.ShouldBeNull();
        _sut.CommittedArmLocation.ShouldBeNull();
    }

    [Fact]
    public void IsWeaponAssigned_WithAssignedWeapon_ShouldReturnTrue()
    {
        // Arrange
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);

        // Act & Assert
        _sut.IsWeaponAssigned(_leftArmWeapon).ShouldBeTrue();
        _sut.IsWeaponAssigned(_leftArmWeapon, _target1).ShouldBeTrue();
        _sut.IsWeaponAssigned(_leftArmWeapon, _target2).ShouldBeFalse();
    }

    [Fact]
    public void IsWeaponAssigned_WithUnassignedWeapon_ShouldReturnFalse()
    {
        // Act & Assert
        _sut.IsWeaponAssigned(_leftArmWeapon).ShouldBeFalse();
        _sut.IsWeaponAssigned(_leftArmWeapon, _target1).ShouldBeFalse();
    }

    [Fact]
    public void SetPrimaryTarget_WithValidTarget_ShouldSetPrimaryTarget()
    {
        // Arrange
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);
        _sut.SetWeaponTarget(_torsoWeapon, _target2, _attacker);

        // Act
        _sut.SetPrimaryTarget(_target2);

        // Assert
        _sut.PrimaryTarget.ShouldBe(_target2);
    }

    [Fact]
    public void SetPrimaryTarget_WithInvalidTarget_ShouldNotSetPrimaryTarget()
    {
        // Arrange
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);

        // Act & Assert
        _sut.SetPrimaryTarget(_target2);
        _sut.PrimaryTarget.ShouldBe(_target1);
    }

    [Fact]
    public void SetPrimaryTarget_ShouldPersistAcrossWeaponChanges()
    {
        // Arrange
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);
        _sut.SetWeaponTarget(_torsoWeapon, _target2, _attacker);
        _sut.SetPrimaryTarget(_target2);

        // Act - Add another weapon to target1
        var rightArmWeapon = CreateWeapon(PartLocation.RightArm, _attacker);
        _sut.SetWeaponTarget(rightArmWeapon, _target1, _attacker);

        // Assert - Primary target should still be target2
        _sut.PrimaryTarget.ShouldBe(_target2);
    }

    [Fact]
    public void UpdatePrimaryTarget_WhenPrimaryTargetRemovedFromTargets_ShouldSelectNewPrimary()
    {
        // Arrange
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);
        _sut.SetWeaponTarget(_torsoWeapon, _target2, _attacker);
        _sut.SetPrimaryTarget(_target2);

        // Act - Remove all weapons targeting target2
        _sut.RemoveWeaponTarget(_torsoWeapon, _attacker);

        // Assert - Primary target should now be target1 (the only remaining target)
        _sut.PrimaryTarget.ShouldBe(_target1);
    }
    
    [Fact]
    public void UpdatePrimaryTarget_WithMultipleTargetsNoPrimary_ShouldSelectForwardArcTarget()
    {
        // Arrange - Set up multiple targets with no primary target set
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);
        _sut.SetWeaponTarget(_torsoWeapon, _target2, _attacker);
        
        // Create a third target positioned in the forward arc (south of attacker)
        var target3 = _mechFactory.Create(MechFactoryTests.CreateDummyMechData());
        target3.Deploy(new HexPosition(new HexCoordinates(1, 0), HexDirection.Bottom));
        var rightArmWeapon = CreateWeapon(PartLocation.RightArm, _attacker);
        _sut.SetWeaponTarget(rightArmWeapon, target3, _attacker);
        
        // Clear primary target to ensure none is set initially
        _sut.SetPrimaryTarget(null);
        
        // Verify we have multiple targets and no primary is set
        _sut.AllTargets.Count().ShouldBe(3);
        _sut.PrimaryTarget.ShouldBeNull();
        
        // Act - This should trigger the forward arc logic in UpdatePrimaryTarget
        // Since no primary target is set, it should select a target from the forward arc
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker); // This calls UpdatePrimaryTarget
        
        // Assert - target1 is backwards to attacker
        _sut.PrimaryTarget.ShouldBe(_target2);
    }
    
    [Fact]
    public void UpdatePrimaryTarget_WithMultipleTargetsNoPrimary_ShouldSelectFirstTarget_WhenAttackerIsNotDeployed()
    {
        // Arrange - Set up multiple targets with no primary target set
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);
        _sut.SetWeaponTarget(_torsoWeapon, _target2, _attacker);
        
        // Create a third target positioned in the forward arc (south of attacker)
        var target3 = _mechFactory.Create(MechFactoryTests.CreateDummyMechData());
        target3.Deploy(new HexPosition(new HexCoordinates(1, 0), HexDirection.Bottom));
        var rightArmWeapon = CreateWeapon(PartLocation.RightArm, _attacker);
        _sut.SetWeaponTarget(rightArmWeapon, target3, _attacker);
        
        // Clear primary target to ensure none is set initially
        _sut.SetPrimaryTarget(null);
        
        // Verify we have multiple targets and no primary is set
        _sut.AllTargets.Count().ShouldBe(3);
        _sut.PrimaryTarget.ShouldBeNull();
        _attacker.RemoveFromBoard();
        
        // Act - This should trigger the forward arc logic in UpdatePrimaryTarget
        // Since no primary target is set and attacker is not deployed there is no way to calculate arcs,
        // and it should fall back to first available target
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker); // This calls UpdatePrimaryTarget
        
        // Assert
        _sut.PrimaryTarget.ShouldBe(_target1);
    }
}
