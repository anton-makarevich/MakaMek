using NSubstitute;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Events;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units;

public class UnitTests
{
    private class TestComponent(string name, int size = 1) : Component(name, [], size)
    {
        public override MakaMekComponent ComponentType => throw new NotImplementedException();
    }

    private class TestWeapon : Weapon
    {
        public TestWeapon(string name, int[] slots, WeaponType type = WeaponType.Energy, MakaMekComponent? ammoType = null) 
            : base(new WeaponDefinition(
            name, 5, 3,
            0, 3, 6, 9, 
            type, 10, 1,1, slots.Length,1,MakaMekComponent.MachineGun,ammoType))
        {
            Mount(slots, null!); // Will be properly mounted later
        }

        public override MakaMekComponent ComponentType => throw new NotImplementedException();
    }
    
    private class TestUnitPart(string name, PartLocation location, int maxArmor, int maxStructure, int slots)
        : UnitPart(name, location, maxArmor, maxStructure, slots)
    {
        internal override bool CanBeBlownOff => true;
    }

    public class TestUnit(
        string chassis,
        string model,
        int tonnage,
        int walkMp,
        IEnumerable<UnitPart> parts,
        Guid? id = null)
        : Unit(chassis, model, tonnage, walkMp, parts, id)
    {
        public override int CalculateBattleValue() => 0;

        public override bool CanMoveBackward(MovementType type) => true;

        public override PartLocation? GetTransferLocation(PartLocation location) =>
            location==PartLocation.CenterTorso
                ?null
                : PartLocation.CenterTorso;

        public override LocationCriticalHitsData CalculateCriticalHitsData(PartLocation location, IDiceRoller diceRoller)
        {
            throw new NotImplementedException();
        }


        protected override void ApplyHeatEffects()
        {
        }
        
        internal override void ApplyArmorAndStructureDamage(int damage, UnitPart targetPart)
        {
            base.ApplyArmorAndStructureDamage(damage, targetPart);
            var destroyedParts = Parts.Where(p => p.IsDestroyed).ToList();
            if (destroyedParts.Count == Parts.Count)
            {
                Status = UnitStatus.Destroyed;
            }
        }

        public void SetCrew(IPilot pilot)
        {
            Crew = pilot;
        }
    }
    
    public static TestUnit CreateTestUnit(Guid? id = null)
    {
        var parts = new List<UnitPart>
        {
            new TestUnitPart("Center Torso", PartLocation.CenterTorso, 10, 5, 10),
            new TestUnitPart("Left Arm", PartLocation.LeftArm, 10, 5, 10),
            new TestUnitPart("Right Arm", PartLocation.RightArm, 10, 5, 10)
        };
        
        return new TestUnit("Test", "Unit", 20, 4, parts, id);
    }
    
    private void MountWeaponOnUnit(TestUnit unit, TestWeapon weapon, PartLocation location, int[] slots)
    {
        var part = unit.Parts.First(p => p.Location == location);
        part.TryAddComponent(weapon,slots);
    }
    
    [Fact]
    public void GetComponentsAtLocation_ShouldReturnComponentsAtSpecifiedLocation()
    {
        // Arrange
        var leftArmPart = new TestUnitPart("Left Arm", PartLocation.LeftArm, 10, 5, 10);
        var rightArmPart = new TestUnitPart("Right Arm", PartLocation.RightArm, 10, 5, 10);
        var testUnit = new TestUnit("Test", "Unit", 20, 4, [leftArmPart, rightArmPart]);
        
        var leftArmComponent1 = new TestComponent("Left Arm Component 1", 2);
        var leftArmComponent2 = new TestComponent("Left Arm Component 2", 2);
        var rightArmComponent = new TestComponent("Right Arm Component", 2);
        
        leftArmPart.TryAddComponent(leftArmComponent1);
        leftArmPart.TryAddComponent(leftArmComponent2);
        rightArmPart.TryAddComponent(rightArmComponent);
        
        // Act
        var leftArmComponents = testUnit.GetComponentsAtLocation(PartLocation.LeftArm).ToList();
        var rightArmComponents = testUnit.GetComponentsAtLocation(PartLocation.RightArm).ToList();
        var headComponents = testUnit.GetComponentsAtLocation(PartLocation.Head).ToList();
        
        // Assert
        leftArmComponents.Count.ShouldBe(2);
        leftArmComponents.ShouldContain(leftArmComponent1);
        leftArmComponents.ShouldContain(leftArmComponent2);
        
        rightArmComponents.Count.ShouldBe(1);
        rightArmComponents.ShouldContain(rightArmComponent);
        
        headComponents.ShouldBeEmpty();
    }
    
    [Fact]
    public void GetComponentsAtLocation_Generic_ShouldReturnComponentsOfSpecificType()
    {
        // Arrange
        var leftArmPart = new TestUnitPart("Left Arm", PartLocation.LeftArm, 10, 5, 10);
        var testUnit = new TestUnit("Test", "Unit", 20, 4, [leftArmPart]);
        
        var component1 = new TestComponent("Component 1", 2);
        var component2 = new TestDerivedComponent("Component 2", 2);
        
        leftArmPart.TryAddComponent(component1);
        leftArmPart.TryAddComponent(component2);
        
        // Act
        var allComponents = testUnit.GetComponentsAtLocation(PartLocation.LeftArm).ToList();
        var derivedComponents = testUnit.GetComponentsAtLocation<TestDerivedComponent>(PartLocation.LeftArm).ToList();
        
        // Assert
        allComponents.Count.ShouldBe(2);
        derivedComponents.Count.ShouldBe(1);
        derivedComponents.ShouldContain(component2);
    }
    
    [Fact]
    public void FindComponentPart_ShouldReturnCorrectPart()
    {
        // Arrange
        var leftArmPart = new TestUnitPart("Left Arm", PartLocation.LeftArm, 10, 5, 10);
        var rightArmPart = new TestUnitPart("Right Arm", PartLocation.RightArm, 10, 5, 10);
        var testUnit = new TestUnit("Test", "Unit", 20, 4, [leftArmPart, rightArmPart]);
        
        var leftArmComponent = new TestComponent("Left Arm Component", 2);
        var rightArmComponent = new TestComponent("Right Arm Component", 2);
        var unmountedComponent = new TestComponent("Unmounted Component", 2);
        
        leftArmPart.TryAddComponent(leftArmComponent);
        rightArmPart.TryAddComponent(rightArmComponent);
        
        // Act
        var leftArmComponentPart = testUnit.FindComponentPart(leftArmComponent);
        var rightArmComponentPart = testUnit.FindComponentPart(rightArmComponent);
        var unmountedComponentPart = testUnit.FindComponentPart(unmountedComponent);
        
        // Assert
        leftArmComponentPart.ShouldBe(leftArmPart);
        rightArmComponentPart.ShouldBe(rightArmPart);
        unmountedComponentPart.ShouldBeNull();
    }
    
    [Fact]
    public void GetMountedComponentAtLocation_ShouldReturnComponentAtSpecificSlots()
    {
        // Arrange
        var unit = CreateTestUnit();
        var weapon1 = new TestWeapon("Weapon 1", [0, 1]);
        var weapon2 = new TestWeapon("Weapon 2", [2, 3]);
        
        MountWeaponOnUnit(unit, weapon1, PartLocation.LeftArm, [0, 1]);
        MountWeaponOnUnit(unit, weapon2, PartLocation.LeftArm, [2, 3]);
        
        // Act
        var foundWeapon1 = unit.GetMountedComponentAtLocation<Weapon>(PartLocation.LeftArm, [0, 1]);
        var foundWeapon2 = unit.GetMountedComponentAtLocation<Weapon>(PartLocation.LeftArm, [2, 3]);
        var notFoundWeapon = unit.GetMountedComponentAtLocation<Weapon>(PartLocation.LeftArm, [4, 5]);
        
        // Assert
        foundWeapon1.ShouldNotBeNull();
        foundWeapon1.ShouldBe(weapon1);
        
        foundWeapon2.ShouldNotBeNull();
        foundWeapon2.ShouldBe(weapon2);
        
        notFoundWeapon.ShouldBeNull();
    }
    
    [Fact]
    public void GetMountedComponentAtLocation_ShouldReturnNull_WhenEmptySlots()
    {
        // Arrange
        var unit = CreateTestUnit();
        var weapon = new TestWeapon("Weapon", [0, 1]);
        MountWeaponOnUnit(unit, weapon, PartLocation.LeftArm, [0, 1]);
        
        // Act
        var result = unit.GetMountedComponentAtLocation<Weapon>(PartLocation.LeftArm, []);
        
        // Assert
        result.ShouldBeNull();
    }
    
    [Fact]
    public void DeclareWeaponAttack_ShouldThrowException_WhenNotDeployed()
    {
        // Arrange
        var unit = CreateTestUnit();
        var targetUnit = CreateTestUnit();
        var weaponTargets = new List<WeaponTargetData>
        {
            new()
            {
                Weapon = new WeaponData
                {
                    Name = "Test Weapon",
                    Location = PartLocation.LeftArm,
                    Slots = [0, 1]
                },
                TargetId = targetUnit.Id,
                IsPrimaryTarget = true
            }
        };
        
        // Act
        var act = () => unit.DeclareWeaponAttack(weaponTargets, [targetUnit]);
        
        // Assert
        var ex = Should.Throw<InvalidOperationException>(act);
        ex.Message.ShouldBe("Unit is not deployed.");
    }
    
    [Fact]
    public void DeclareWeaponAttack_ShouldAssignTargetsToWeapons()
    {
        // Arrange
        var attackerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        
        var attacker = CreateTestUnit(attackerId);
        var target = CreateTestUnit(targetId);
        
        var weapon = new TestWeapon("Test Weapon", [0, 1]);
        MountWeaponOnUnit(attacker, weapon, PartLocation.LeftArm, [0, 1]);
        
        attacker.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        target.Deploy(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top));
        
        var weaponTargets = new List<WeaponTargetData>
        {
            new()
            {
                Weapon = new WeaponData
                {
                    Name = "Test Weapon",
                    Location = PartLocation.LeftArm,
                    Slots = [0, 1]
                },
                TargetId = targetId,
                IsPrimaryTarget = true
            }
        };
        
        // Act
        attacker.DeclareWeaponAttack(weaponTargets, [target]);
        
        // Assert
        weapon.Target.ShouldNotBeNull();
        weapon.Target.ShouldBe(target);
        attacker.HasDeclaredWeaponAttack.ShouldBeTrue();
    }
    
    [Fact]
    public void DeclareWeaponAttack_ShouldHandleMultipleWeapons()
    {
        // Arrange
        var attackerId = Guid.NewGuid();
        var targetId1 = Guid.NewGuid();
        var targetId2 = Guid.NewGuid();
        
        var attacker = CreateTestUnit(attackerId);
        var target1 = CreateTestUnit(targetId1);
        var target2 = CreateTestUnit(targetId2);
        
        var weapon1 = new TestWeapon("Weapon 1", [0, 1]);
        var weapon2 = new TestWeapon("Weapon 2", [2, 3]);
        
        MountWeaponOnUnit(attacker, weapon1, PartLocation.LeftArm, [0, 1]);
        MountWeaponOnUnit(attacker, weapon2, PartLocation.RightArm, [2, 3]);
        
        attacker.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        target1.Deploy(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top));
        target2.Deploy(new HexPosition(new HexCoordinates(1, 3), HexDirection.Top));
        
        var weaponTargets = new List<WeaponTargetData>
        {
            new()
            {
                Weapon = new WeaponData
                {
                    Name = "Weapon 1",
                    Location = PartLocation.LeftArm,
                    Slots = [0, 1]
                },
                TargetId = targetId1,
                IsPrimaryTarget = true
            },
            new()
            {
                Weapon = new WeaponData
                {
                    Name = "Weapon 2",
                    Location = PartLocation.RightArm,
                    Slots = [2, 3]
                },
                TargetId = targetId2,
                IsPrimaryTarget = true
            }
        };
        
        // Act
        attacker.DeclareWeaponAttack(weaponTargets, [target1, target2]);
        
        // Assert
        weapon1.Target.ShouldNotBeNull();
        weapon1.Target.ShouldBe(target1);
        
        weapon2.Target.ShouldNotBeNull();
        weapon2.Target.ShouldBe(target2);
        
        attacker.HasDeclaredWeaponAttack.ShouldBeTrue();
    }
    
    [Fact]
    public void DeclareWeaponAttack_ShouldSkipWeaponsNotFound()
    {
        // Arrange
        var attackerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        
        var attacker = CreateTestUnit(attackerId);
        var target = CreateTestUnit(targetId);
        
        var weapon = new TestWeapon("Test Weapon", [0, 1]);
        MountWeaponOnUnit(attacker, weapon, PartLocation.LeftArm, [0, 1]);
        
        attacker.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        target.Deploy(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top));
        
        var weaponTargets = new List<WeaponTargetData>
        {
            new()
            {
                Weapon = new WeaponData
                {
                    Name = "Test Weapon",
                    Location = PartLocation.LeftArm,
                    Slots = [0, 1]
                },
                TargetId = targetId,
                IsPrimaryTarget = true
            },
            new()
            {
                Weapon = new WeaponData
                {
                    Name = "Non-existent Weapon",
                    Location = PartLocation.RightArm,
                    Slots = [4, 5]
                },
                TargetId = targetId,
                IsPrimaryTarget = false
            }
        };
        
        // Act
        attacker.DeclareWeaponAttack(weaponTargets, [target]);
        
        // Assert
        weapon.Target.ShouldNotBeNull();
        weapon.Target.ShouldBe(target);
        attacker.HasDeclaredWeaponAttack.ShouldBeTrue();
    }
    
    [Fact]
    public void DeclareWeaponAttack_ShouldSkipTargetsNotFound()
    {
        // Arrange
        var attackerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var nonExistentTargetId = Guid.NewGuid();
        
        var attacker = CreateTestUnit(attackerId);
        var target = CreateTestUnit(targetId);
        
        var weapon1 = new TestWeapon("Weapon 1", [0, 1]);
        var weapon2 = new TestWeapon("Weapon 2", [2, 3]);
        
        MountWeaponOnUnit(attacker, weapon1, PartLocation.LeftArm, [0, 1]);
        MountWeaponOnUnit(attacker, weapon2, PartLocation.RightArm, [2, 3]);
        
        attacker.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        target.Deploy(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top));
        
        var weaponTargets = new List<WeaponTargetData>
        {
            new()
            {
                Weapon = new WeaponData
                {
                    Name = "Weapon 1",
                    Location = PartLocation.LeftArm,
                    Slots = [0, 1]
                },
                TargetId = targetId,
                IsPrimaryTarget = true
            },
            new()
            {
                Weapon = new WeaponData
                {
                    Name = "Weapon 2",
                    Location = PartLocation.RightArm,
                    Slots = [2, 3]
                },
                TargetId = nonExistentTargetId,
                IsPrimaryTarget = true
            }
        };
        
        // Act
        attacker.DeclareWeaponAttack(weaponTargets, [target]);
        
        // Assert
        weapon1.Target.ShouldNotBeNull();
        weapon1.Target.ShouldBe(target);
        
        weapon2.Target.ShouldBeNull();
        
        attacker.HasDeclaredWeaponAttack.ShouldBeTrue();
    }
    
    [Fact]
    public void GetComponentsAtLocation_ReturnsEmptyCollection_WhenLocationNotFound()
    {
        // Arrange
        var testUnit = CreateTestUnit();

        // Act
        var components = testUnit.GetComponentsAtLocation(PartLocation.Head);

        // Assert
        components.ShouldBeEmpty();
    }
    
    [Fact]
    public void GetAmmoForWeapon_ReturnsEmptyCollection_WhenWeaponDoesNotRequireAmmo()
    {
        // Arrange
        var testUnit = CreateTestUnit();
        var energyWeapon = new TestWeapon("Energy Weapon", [0, 1]);
        
        // Act
        var ammo = testUnit.GetAmmoForWeapon(energyWeapon);
        
        // Assert
        ammo.ShouldBeEmpty();
    }
    
    [Fact]
    public void GetAmmoForWeapon_ReturnsMatchingAmmo_WhenWeaponRequiresAmmo()
    {
        // Arrange
        var centerTorso = new TestUnitPart("Center Torso", PartLocation.CenterTorso, 10, 5, 10);
        var leftTorso = new TestUnitPart("Left Torso", PartLocation.LeftTorso, 10, 5, 10);
        var testUnit = new TestUnit("Test", "Unit", 20, 4, [centerTorso, leftTorso]);
        
        var ac5Weapon = new TestWeapon("AC/5", [0, 1], WeaponType.Ballistic, MakaMekComponent.ISAmmoAC5);
        var ac5Ammo1 = new Ammo(Ac5.Definition, 20);
        var ac5Ammo2 = new Ammo(Ac5.Definition, 20);
        var lrm5Ammo = new Ammo(Lrm5.Definition, 24);
        
        centerTorso.TryAddComponent(ac5Weapon);
        centerTorso.TryAddComponent(ac5Ammo1);
        leftTorso.TryAddComponent(ac5Ammo2);
        leftTorso.TryAddComponent(lrm5Ammo);
        
        // Act
        var ammo = testUnit.GetAmmoForWeapon(ac5Weapon).ToList();
        
        // Assert
        ammo.Count.ShouldBe(2);
        ammo.ShouldContain(ac5Ammo1);
        ammo.ShouldContain(ac5Ammo2);
        ammo.ShouldNotContain(lrm5Ammo);
    }
    
    [Fact]
    public void GetRemainingAmmoShots_ReturnsNegativeOne_WhenWeaponDoesNotRequireAmmo()
    {
        // Arrange
        var testUnit = CreateTestUnit();
        var energyWeapon = new TestWeapon("Energy Weapon", [0, 1]);
        
        // Act
        var remainingShots = testUnit.GetRemainingAmmoShots(energyWeapon);
        
        // Assert
        remainingShots.ShouldBe(-1);
    }
    
    [Fact]
    public void GetRemainingAmmoShots_ReturnsSumOfAmmo_WhenWeaponRequiresAmmo()
    {
        // Arrange
        var centerTorso = new TestUnitPart("Center Torso", PartLocation.CenterTorso, 10, 5, 10);
        var leftTorso = new TestUnitPart("Left Torso", PartLocation.LeftTorso, 10, 5, 10);
        var testUnit = new TestUnit("Test", "Unit", 20, 4, [centerTorso, leftTorso]);
        
        var ac5Weapon = new TestWeapon("AC/5", [0, 1], WeaponType.Ballistic, MakaMekComponent.ISAmmoAC5);
        var ac5Ammo1 = new Ammo(Ac5.Definition, 20);
        var ac5Ammo2 = new Ammo(Ac5.Definition, 15);
        
        centerTorso.TryAddComponent(ac5Weapon);
        centerTorso.TryAddComponent(ac5Ammo1);
        leftTorso.TryAddComponent(ac5Ammo2);
        
        // Act
        var remainingShots = testUnit.GetRemainingAmmoShots(ac5Weapon);
        
        // Assert
        remainingShots.ShouldBe(35); // 20 + 15
    }
    
    [Fact]
    public void GetRemainingAmmoShots_ReturnsZero_WhenNoAmmoAvailable()
    {
        // Arrange
        var centerTorso = new TestUnitPart("Center Torso", PartLocation.CenterTorso, 10, 5, 10);
        var testUnit = new TestUnit("Test", "Unit", 20, 4, [centerTorso]);
        
        var ac5Weapon = new TestWeapon("AC/5", [0, 1], WeaponType.Ballistic, MakaMekComponent.ISAmmoAC5);
        centerTorso.TryAddComponent(ac5Weapon);
        
        // Act
        var remainingShots = testUnit.GetRemainingAmmoShots(ac5Weapon);
        
        // Assert
        remainingShots.ShouldBe(0);
    }
    
    [Fact]
    public void ApplyDamage_WithHitLocationsList_ShouldApplyDamageToCorrectParts()
    {
        // Arrange
        var unit = CreateTestUnit();
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.CenterTorso, 5, []),
            new(PartLocation.LeftArm, 3, [])
        };
        
        // Get initial armor values
        var centerTorsoPart = unit.Parts.First(p => p.Location == PartLocation.CenterTorso);
        var leftArmPart = unit.Parts.First(p => p.Location == PartLocation.LeftArm);
        var initialCenterTorsoArmor = centerTorsoPart.CurrentArmor;
        var initialLeftArmArmor = leftArmPart.CurrentArmor;
        
        // Act
        unit.ApplyDamage(hitLocations);
        
        // Assert
        centerTorsoPart.CurrentArmor.ShouldBe(initialCenterTorsoArmor - 5);
        leftArmPart.CurrentArmor.ShouldBe(initialLeftArmArmor - 3);
    }
    
    [Fact]
    public void ApplyDamage_WithHitLocationsList_ShouldIgnoreNonExistentParts()
    {
        // Arrange
        var unit = CreateTestUnit();
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.CenterTorso, 5, []),
            new(PartLocation.Head, 3, []) // Unit doesn't have a Head part
        };
        
        // Get initial armor values
        var centerTorsoPart = unit.Parts.First(p => p.Location == PartLocation.CenterTorso);
        var initialCenterTorsoArmor = centerTorsoPart.CurrentArmor;
        
        // Act
        unit.ApplyDamage(hitLocations);
        
        // Assert
        centerTorsoPart.CurrentArmor.ShouldBe(initialCenterTorsoArmor - 5);
        // No exception should be thrown for the non-existent part
    }
    
    [Fact]
    public void ApplyDamage_WithEmptyHitLocationsList_ShouldNotChangeArmor()
    {
        // Arrange
        var unit = CreateTestUnit();
        var hitLocations = new List<HitLocationData>();
        
        // Get initial armor values for all parts
        var initialArmorValues = unit.Parts.ToDictionary(p => p.Location, p => p.CurrentArmor);
        
        // Act
        unit.ApplyDamage(hitLocations);
        
        // Assert
        foreach (var part in unit.Parts)
        {
            part.CurrentArmor.ShouldBe(initialArmorValues[part.Location]);
        }
    }
    
    [Fact]
    public void TotalMaxArmor_ShouldReturnSumOfAllPartsMaxArmor()
    {
        // Arrange
        var parts = new List<UnitPart>
        {
            new TestUnitPart("Center Torso", PartLocation.CenterTorso, 10, 5, 10),
            new TestUnitPart("Left Arm", PartLocation.LeftArm, 15, 5, 10),
            new TestUnitPart("Right Arm", PartLocation.RightArm, 20, 5, 10)
        };
        
        var unit = new TestUnit("Test", "Unit", 20, 4, parts);
        
        // Act
        var totalMaxArmor = unit.TotalMaxArmor;
        
        // Assert
        totalMaxArmor.ShouldBe(45); // 10 + 15 + 20
    }
    
    [Fact]
    public void TotalCurrentArmor_ShouldReturnSumOfAllPartsCurrentArmor()
    {
        // Arrange
        var parts = new List<UnitPart>
        {
            new TestUnitPart("Center Torso", PartLocation.CenterTorso, 10, 5, 10),
            new TestUnitPart("Left Arm", PartLocation.LeftArm, 15, 5, 10),
            new TestUnitPart("Right Arm", PartLocation.RightArm, 20, 5, 10)
        };
        
        var unit = new TestUnit("Test", "Unit", 20, 4, parts);
        
        // Apply damage to reduce armor
        unit.ApplyArmorAndStructureDamage(5, parts[0]); // Center Torso: 10 -> 5
        unit.ApplyArmorAndStructureDamage(10, parts[1]); // Left Arm: 15 -> 5
        
        // Act
        var totalCurrentArmor = unit.TotalCurrentArmor;
        
        // Assert
        totalCurrentArmor.ShouldBe(30); // 5 + 5 + 20
    }
    
    [Fact]
    public void TotalMaxStructure_ShouldReturnSumOfAllPartsMaxStructure()
    {
        // Arrange
        var parts = new List<UnitPart>
        {
            new TestUnitPart("Center Torso", PartLocation.CenterTorso, 10, 5, 10),
            new TestUnitPart("Left Arm", PartLocation.LeftArm, 15, 8, 10),
            new TestUnitPart("Right Arm", PartLocation.RightArm, 20, 12, 10)
        };
        
        var unit = new TestUnit("Test", "Unit", 20, 4, parts);
        
        // Act
        var totalMaxStructure = unit.TotalMaxStructure;
        
        // Assert
        totalMaxStructure.ShouldBe(25); // 5 + 8 + 12
    }
    
    [Fact]
    public void TotalCurrentStructure_ShouldReturnSumOfAllPartsCurrentStructure()
    {
        // Arrange
        var parts = new List<UnitPart>
        {
            new TestUnitPart("Center Torso", PartLocation.CenterTorso, 10, 5, 10),
            new TestUnitPart("Left Arm", PartLocation.LeftArm, 15, 8, 10),
            new TestUnitPart("Right Arm", PartLocation.RightArm, 20, 12, 10)
        };
        
        var unit = new TestUnit("Test", "Unit", 20, 4, parts);
        
        // Apply damage to reduce armor and structure
        unit.ApplyArmorAndStructureDamage(15, parts[0]); // Center Torso: 10 armor -> 0, 5 structure -> 0
        unit.ApplyArmorAndStructureDamage(20, parts[1]); // Left Arm: 15 armor -> 0, 8 structure -> 3
        
        // Act
        var totalCurrentStructure = unit.TotalCurrentStructure;
        
        // Assert
        totalCurrentStructure.ShouldBe(15); // 0 + 3 + 12
    }
    
    [Fact]
    public void ArmorAndStructure_ShouldUpdateCorrectly_WhenDamageIsApplied()
    {
        // Arrange
        var parts = new List<UnitPart>
        {
            new TestUnitPart("Center Torso", PartLocation.CenterTorso, 10, 5, 10),
            new TestUnitPart("Left Arm", PartLocation.LeftArm, 15, 8, 10),
            new TestUnitPart("Right Arm", PartLocation.RightArm, 20, 12, 10)
        };
        
        var unit = new TestUnit("Test", "Unit", 20, 4, parts);
        
        // Initial values
        unit.TotalMaxArmor.ShouldBe(45); // 10 + 15 + 20
        unit.TotalCurrentArmor.ShouldBe(45);
        unit.TotalMaxStructure.ShouldBe(25); // 5 + 8 + 12
        unit.TotalCurrentStructure.ShouldBe(25);
        
        // Act - Apply damage to one part
        unit.ApplyArmorAndStructureDamage(5, parts[0]); // Reduce Center Torso armor by 5
        
        // Assert - Check updated values
        unit.TotalCurrentArmor.ShouldBe(40); // 5 + 15 + 20
        unit.TotalCurrentStructure.ShouldBe(25); // Structure unchanged
        
        // Act - Apply more damage to penetrate armor and damage structure
        unit.ApplyArmorAndStructureDamage(8, parts[0]); // Reduce remaining CT armor (5) and damage structure (3)
        
        // Assert - Check updated values
        unit.TotalCurrentArmor.ShouldBe(35); // 0 + 15 + 20
        unit.TotalCurrentStructure.ShouldBe(22); // 2 + 8 + 12
    }
    
    [Fact]
    public void FireWeapon_UseAmmo_ForBallisticWeapon()
    {
        // Arrange
        var unit = CreateTestUnit();
        var ballisticWeapon = new TestWeapon("Ballistic Weapon", [0, 1], WeaponType.Ballistic, MakaMekComponent.ISAmmoAC5);
        MountWeaponOnUnit(unit, ballisticWeapon, PartLocation.LeftArm, [0, 1]);
        
        // Add ammo to the unit
        var ammo = new Ammo(Ac5.Definition, 10);
        var rightArmPart = unit.Parts.First(p => p.Location == PartLocation.RightArm);
        rightArmPart.TryAddComponent(ammo);
        
        var weaponData = new WeaponData
        {
            Name = ballisticWeapon.Name,
            Location = PartLocation.LeftArm,
            Slots = [0, 1]
        };

        var initialAmmoShots = ammo.RemainingShots;
        
        // Act
        unit.FireWeapon(weaponData);
        
        // Assert
        ammo.RemainingShots.ShouldBe(initialAmmoShots - 1);
    }
    
    [Fact]
    public void FireWeapon_ShouldNotFire_WhenWeaponNotFound()
    {
        // Arrange
        var unit = CreateTestUnit();
        
        var weaponData = new WeaponData
        {
            Name = "Non-existent Weapon",
            Location = PartLocation.LeftArm,
            Slots = [0, 1]
        };
        
        var initialHeat = unit.CurrentHeat;
        
        // Act
        unit.FireWeapon(weaponData);
        
        // Assert
        unit.CurrentHeat.ShouldBe(initialHeat); // Heat should not change
    }
    
    [Fact]
    public void FireWeapon_ShouldNotFire_WhenWeaponDestroyed()
    {
        // Arrange
        var unit = CreateTestUnit();
        var weapon = new TestWeapon("Test Weapon", [0, 1]);
        MountWeaponOnUnit(unit, weapon, PartLocation.LeftArm, [0, 1]);
        
        // Destroy the weapon
        weapon.Hit();
        
        var weaponData = new WeaponData
        {
            Name = weapon.Name,
            Location = PartLocation.LeftArm,
            Slots = [0, 1]
        };
        
        var initialHeat = unit.CurrentHeat;
        
        // Act
        unit.FireWeapon(weaponData);
        
        // Assert
        unit.CurrentHeat.ShouldBe(initialHeat); // Heat should not change
    }
    
    [Fact]
    public void FireWeapon_ShouldUseAmmoWithMostShots_WhenMultipleAmmoAvailable()
    {
        // Arrange
        var unit = CreateTestUnit();
        var ballisticWeapon = new TestWeapon("Ballistic Weapon", [0, 1], WeaponType.Ballistic, MakaMekComponent.ISAmmoAC5);
        MountWeaponOnUnit(unit, ballisticWeapon, PartLocation.LeftArm, [0, 1]);
        
        // Add multiple ammo components with different shot counts
        var ammo1 = new Ammo(Ac5.Definition, 3);
        var ammo2 = new Ammo(Ac5.Definition, 8); // This one has more shots
        var ammo3 = new Ammo(Ac5.Definition, 5);
        
        var rightArmPart = unit.Parts.First(p => p.Location == PartLocation.RightArm);
        rightArmPart.TryAddComponent(ammo1);
        rightArmPart.TryAddComponent(ammo2);
        rightArmPart.TryAddComponent(ammo3);
        
        var weaponData = new WeaponData
        {
            Name = ballisticWeapon.Name,
            Location = PartLocation.LeftArm,
            Slots = [0, 1]
        };
        
        // Act
        unit.FireWeapon(weaponData);
        
        // Assert
        ammo1.RemainingShots.ShouldBe(3); // Unchanged
        ammo2.RemainingShots.ShouldBe(7); // Reduced by 1
        ammo3.RemainingShots.ShouldBe(5); // Unchanged
    }
    
    [Fact]
    public void GetHeatData_WithNoHeatSources_ReturnsExpectedData()
    {
        // Arrange
        var unit = CreateTestUnit();
        var rulesProvider = Substitute.For<IRulesProvider>();
        
        // Act
        var heatData = unit.GetHeatData(rulesProvider);
        
        // Assert
        heatData.MovementHeatSources.ShouldBeEmpty();
        heatData.WeaponHeatSources.ShouldBeEmpty();
        heatData.TotalHeatPoints.ShouldBe(0);
        heatData.DissipationData.HeatSinks.ShouldBe(unit.GetAllComponents<HeatSink>().Count());
        heatData.DissipationData.EngineHeatSinks.ShouldBe(0); // Default engine heat sinks for base unit (Mech overrides with 10)
        heatData.DissipationData.DissipationPoints.ShouldBe(unit.HeatDissipation);
    }
    
    [Fact]
    public void GetHeatData_WithMovementHeat_ReturnsExpectedData()
    {
        // Arrange
        var unit = CreateTestUnit();
        var rulesProvider = new ClassicBattletechRulesProvider();
        var deployPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        unit.Deploy(deployPosition);
        
        // Move the unit with Run movement type
        unit.Move(MovementType.Run, [
            new PathSegmentData
            {
                From = deployPosition.ToData(),
                To = deployPosition.ToData(), // Fixed: Using proper HexPositionData
                Cost = 5
            }
        ]);
        
        // Act
        var heatData = unit.GetHeatData(rulesProvider);
        
        // Assert
        heatData.MovementHeatSources.ShouldNotBeEmpty();
        heatData.MovementHeatSources.Count.ShouldBe(1);
        heatData.MovementHeatSources[0].MovementType.ShouldBe(MovementType.Run);
        heatData.MovementHeatSources[0].MovementPointsSpent.ShouldBe(5);
        heatData.WeaponHeatSources.ShouldBeEmpty();
        heatData.TotalHeatPoints.ShouldBe(heatData.MovementHeatSources[0].HeatPoints);
    }
    
    [Fact]
    public void GetHeatData_WithWeaponHeat_ReturnsExpectedData()
    {
        // Arrange
        var unit = CreateTestUnit();
        var targetUnit = CreateTestUnit();
        var rulesProvider = Substitute.For<IRulesProvider>();
        
        // Add a weapon to the unit
        var weapon = new TestWeapon("Test Laser", [3]);
        MountWeaponOnUnit(unit, weapon, PartLocation.RightArm,[3]);
        
        // Set the weapon's target
        weapon.Target = targetUnit;
        
        // Act
        var heatData = unit.GetHeatData(rulesProvider);
        
        // Assert
        heatData.MovementHeatSources.ShouldBeEmpty();
        heatData.WeaponHeatSources.ShouldNotBeEmpty();
        heatData.WeaponHeatSources.Count.ShouldBe(1);
        heatData.WeaponHeatSources[0].WeaponName.ShouldBe("Test Laser");
        heatData.WeaponHeatSources[0].HeatPoints.ShouldBe(weapon.Heat);
        heatData.TotalHeatPoints.ShouldBe(weapon.Heat);
    }
    
    [Fact]
    public void GetHeatData_WithCombinedHeatSources_ReturnsExpectedData()
    {
        // Arrange
        var unit = CreateTestUnit();
        var targetUnit = CreateTestUnit();
        var rulesProvider = new ClassicBattletechRulesProvider();
        
        // Add a weapon to the unit
        var weapon = new TestWeapon("Test Laser", [3]);
        MountWeaponOnUnit(unit, weapon, PartLocation.RightArm,[3]);
        
        // Set the weapon's target
        weapon.Target = targetUnit;
        
        // Deploy and move the unit
        var deployPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        unit.Deploy(deployPosition);
        
        // Move the unit with Jump movement type
        unit.Move(MovementType.Jump, [
            new PathSegmentData
            {
                From = deployPosition.ToData(),
                To = deployPosition.ToData(), // Fixed: Using proper HexPositionData
                Cost = 3
            }
        ]);
        
        // Act
        var heatData = unit.GetHeatData(rulesProvider);
        
        // Assert
        heatData.MovementHeatSources.ShouldNotBeEmpty();
        heatData.MovementHeatSources.Count.ShouldBe(1);
        heatData.MovementHeatSources[0].MovementType.ShouldBe(MovementType.Jump);
        
        heatData.WeaponHeatSources.ShouldNotBeEmpty();
        heatData.WeaponHeatSources.Count.ShouldBe(1);
        heatData.WeaponHeatSources[0].WeaponName.ShouldBe("Test Laser");
        
        // Total heat should be the sum of movement and weapon heat
        heatData.TotalHeatPoints.ShouldBe(
            heatData.MovementHeatSources[0].HeatPoints + 
            heatData.WeaponHeatSources[0].HeatPoints);
    }
    
    [Fact]
    public void GetHeatData_WithHeatSinks_ReturnsCorrectDissipationData()
    {
        // Arrange
        var unit = CreateTestUnit();
        var rulesProvider = Substitute.For<IRulesProvider>();
        
        // Add heat sinks to the unit
        var rightArmPart = unit.Parts.First(p => p.Location == PartLocation.RightArm);
        rightArmPart.TryAddComponent(new HeatSink());
        rightArmPart.TryAddComponent(new HeatSink());
        
        // Act
        var heatData = unit.GetHeatData(rulesProvider);
        
        // Assert
        var expectedHeatSinks = unit.GetAllComponents<HeatSink>().Count();
        heatData.DissipationData.HeatSinks.ShouldBe(expectedHeatSinks);
        heatData.DissipationData.EngineHeatSinks.ShouldBe(0); // Default engine heat sinks for base unit (Mech overrides with 10)
        heatData.DissipationData.DissipationPoints.ShouldBe(unit.HeatDissipation);
        heatData.TotalHeatDissipationPoints.ShouldBe(unit.HeatDissipation);
    }
    
    [Fact]
    public void GetHeatData_WithHeatSinks_ReturnsDissipationDataConsidderingActiveHeatSinksOnly()
    {
        // Arrange
        var unit = CreateTestUnit();
        var rulesProvider = Substitute.For<IRulesProvider>();
        
        // Add heat sinks to the unit
        var rightArmPart = unit.Parts.First(p => p.Location == PartLocation.RightArm);
        var destroyedHeatSink = new HeatSink();
        destroyedHeatSink.Hit();
        rightArmPart.TryAddComponent(destroyedHeatSink);
        rightArmPart.TryAddComponent(new HeatSink());
        
        // Act
        var heatData = unit.GetHeatData(rulesProvider);
        
        // Assert
        heatData.DissipationData.HeatSinks.ShouldBe(1); //another one is destroyed
        heatData.DissipationData.EngineHeatSinks.ShouldBe(0); // Default engine heat sinks
        heatData.DissipationData.DissipationPoints.ShouldBe(1);
    }
    
    [Fact]
    public void GetHeatData_DoesNotCountHeatSinksOnDestroyedParts()
    {
        // Arrange
        var unit = CreateTestUnit();
        var rulesProvider = Substitute.For<IRulesProvider>();
        
        // Add heat sinks to the unit
        var rightArmPart = unit.Parts.First(p => p.Location == PartLocation.RightArm);
        rightArmPart.TryAddComponent(new HeatSink());
        rightArmPart.TryAddComponent(new HeatSink());
        rightArmPart.BlowOff(); // destroy the part
        
        // Act
        var heatData = unit.GetHeatData(rulesProvider);
        
        // Assert
        heatData.DissipationData.HeatSinks.ShouldBe(0);
        heatData.DissipationData.EngineHeatSinks.ShouldBe(0); // Default engine heat sinks for generic unit
        heatData.DissipationData.DissipationPoints.ShouldBe(0);
    }
    
    [Fact]
    public void HasAvailableComponent_WithAvailableComponent_ReturnsTrue()
    {
        // Arrange
        var unit = CreateTestUnit();
        var testComponent = new TestComponent("Test Component");
        var part = unit.Parts.First(p => p.Location == PartLocation.LeftArm);
        part.TryAddComponent(testComponent);
        
        // Act
        var result = unit.HasAvailableComponent<TestComponent>();
        
        // Assert
        result.ShouldBeTrue();
    }
    
    [Theory]
    [InlineData(true, false)]  // Destroyed component
    [InlineData(false, true)]  // Deactivated component
    public void HasAvailableComponent_WithUnavailableComponent_ReturnsFalse(bool isDestroyed, bool deactivate)
    {
        // Arrange
        var unit = CreateTestUnit();
        var testComponent = new TestComponent("Test Component");
        var part = unit.Parts.First(p => p.Location == PartLocation.LeftArm);
        part.TryAddComponent(testComponent);
        
        // Make the component unavailable
        if (isDestroyed)
        {
            testComponent.Hit();
        }
        
        if (deactivate)
        {
            testComponent.Deactivate();
        }
        
        // Act
        var result = unit.HasAvailableComponent<TestComponent>();
        
        // Assert
        result.ShouldBeFalse();
    }
    
    [Fact]
    public void ApplyDamage_WithCriticalHits_DestroysComponentAtSlot()
    {
        // Arrange
        var leftArm = new TestUnitPart("Left Arm", PartLocation.LeftArm, 10, 5, 5);
        var critComponent = new TestComponent("CritComp");
        leftArm.TryAddComponent(critComponent, [2]);
        var unit = new TestUnit("Test", "Unit", 20, 4, [leftArm]);
        var hitLocation = new HitLocationData(PartLocation.LeftArm, 0, [], 
            [new LocationCriticalHitsData(PartLocation.LeftArm, 10, 1, 
                [CreateComponentHitData(2)])]);
    
        // Pre-assert: component is not destroyed
        critComponent.IsDestroyed.ShouldBeFalse();
        // Act
        unit.ApplyDamage([hitLocation]);
        // Assert
        critComponent.IsDestroyed.ShouldBeTrue();
    }
    
    [Fact]
    public void ApplyDamage_WithBlownOff_DestroysTheWholePart()
    {
        // Arrange
        var leftArm = new TestUnitPart("Left Arm", PartLocation.LeftArm, 10, 5, 5);
        var unit = new TestUnit("Test", "Unit", 20, 4, [leftArm]);
        var hitLocation = new HitLocationData(PartLocation.LeftArm, 0, [],
            [new LocationCriticalHitsData(PartLocation.LeftArm, 10, 1, null,true)]);
        
        // Act
        unit.ApplyDamage([hitLocation]);
        // Assert
        leftArm.IsDestroyed.ShouldBeTrue();
    }
    
    [Fact]
    public void ApplyDamage_ShouldApplyCriticalHits_WhenCriticalHitsArePresent()
    {
        // Arrange
        var unit = CreateTestUnit();
        var targetPart = unit.Parts.First(p => p.Location == PartLocation.LeftArm);
        var component = new TestComponent("Test Component", 3);
        targetPart.TryAddComponent(component);
        
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.LeftArm, 10,[new DiceResult(3),new DiceResult(5)],
                [new LocationCriticalHitsData(PartLocation.LeftArm, 10, 2, 
                    [CreateComponentHitData(0), CreateComponentHitData(2)])])
        };
        
        // Act
        unit.ApplyDamage(hitLocations);
        
        // Assert
        targetPart.HitSlots.Count.ShouldBe(2);
        targetPart.HitSlots.ShouldContain(0);
        targetPart.HitSlots.ShouldContain(2);
        component.IsDestroyed.ShouldBeTrue();
    }
    
    [Fact]
    public void ApplyDamage_ShouldBlowOffPart_WhenCriticalHitsBlownOffIsTrue()
    {
        // Arrange
        var unit = CreateTestUnit();
        var targetPart = unit.Parts.First(p => p.Location == PartLocation.LeftArm);
        
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.LeftArm, 10, [new DiceResult(3),new DiceResult(5)],
                [new LocationCriticalHitsData(PartLocation.LeftArm, 12, 0, null, true)])
        };
        
        // Act
        unit.ApplyDamage(hitLocations);
        
        // Assert
        targetPart.IsBlownOff.ShouldBeTrue();
    }
    
    [Fact]
    public void ApplyDamage_ShouldNotApplyCriticalHits_WhenCriticalHitsAreNull()
    {
        // Arrange
        var unit = CreateTestUnit();
        var targetPart = unit.Parts.First(p => p.Location == PartLocation.LeftArm);
        var component = new TestComponent("Test Component", 3);
        targetPart.TryAddComponent(component);
        
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.LeftArm, 10, [new DiceResult(3),new DiceResult(5)])
        };
        
        // Act
        unit.ApplyDamage(hitLocations);
        
        // Assert
        targetPart.HitSlots.Count.ShouldBe(0);
        component.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void ApplyDamage_WithExplodableComponent_ShouldAddExplosionDamage()
    {
        // Arrange
        var unit = CreateTestUnit();
        var targetPart = unit.Parts.First(p => p.Location == PartLocation.LeftArm);
        
        // Create an explodable component
        var explodableComponent = new TestExplodableComponent("Explodable Component", 5);
        targetPart.TryAddComponent(explodableComponent, [1]);
        
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.LeftArm, 5, [new DiceResult(3)],
                [new LocationCriticalHitsData(PartLocation.LeftArm, 0, 1, 
                    [CreateComponentHitData(1)])])
        };
        
        // Pre-assert: component has not exploded
        explodableComponent.HasExploded.ShouldBeFalse();
        targetPart.CurrentArmor.ShouldBe(10);
        
        // Act
        unit.ApplyDamage(hitLocations);
        
        // Assert
        explodableComponent.HasExploded.ShouldBeTrue();
        // Verify that the total damage applied was the initial damage (5) plus the explosion damage (5)
        targetPart.CurrentArmor.ShouldBe(0); // 10 - (5 + 5) = 0
    }
    
    [Fact]
    public void ApplyDamage_WithMultipleExplodableComponents_ShouldAddAllExplosionDamage()
    {
        // Arrange
        var unit = CreateTestUnit();
        var targetPart = unit.Parts.First(p => p.Location == PartLocation.LeftArm);
        
        // Create multiple explodable components
        var explodableComponent1 = new TestExplodableComponent("Explodable Component 1", 3);
        var explodableComponent2 = new TestExplodableComponent("Explodable Component 2", 4);
        targetPart.TryAddComponent(explodableComponent1, [1]);
        targetPart.TryAddComponent(explodableComponent2, [2]);
        
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.LeftArm, 2, [new DiceResult(3)],
                [new LocationCriticalHitsData(PartLocation.LeftArm, 0, 1, 
                    [CreateComponentHitData(1), CreateComponentHitData(2)])])
        };
        
        // Pre-assert: components have not exploded
        explodableComponent1.HasExploded.ShouldBeFalse();
        explodableComponent2.HasExploded.ShouldBeFalse();
        targetPart.CurrentArmor.ShouldBe(10);
        
        // Act
        unit.ApplyDamage(hitLocations);
        
        // Assert
        explodableComponent1.HasExploded.ShouldBeTrue();
        explodableComponent2.HasExploded.ShouldBeTrue();
        // Verify that the total damage applied was the initial damage (2) plus the explosion damage (3 + 4 = 7)
        targetPart.CurrentArmor.ShouldBe(1); // 10 - (2 + 3 + 4) = 1
    }
    
    [Fact]
    public void ApplyDamage_WithAlreadyExplodedComponent_ShouldNotAddExplosionDamageAgain()
    {
        // Arrange
        var unit = CreateTestUnit();
        var targetPart = unit.Parts.First(p => p.Location == PartLocation.LeftArm);
        
        // Create an explodable component that has already exploded
        var explodableComponent = new TestExplodableComponent("Explodable Component", 5);
        explodableComponent.Hit(); // This will set HasExploded to true
        targetPart.TryAddComponent(explodableComponent, [1]);
        
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.LeftArm, 3, [new DiceResult(3)],
                [new LocationCriticalHitsData(PartLocation.LeftArm, 0, 1, 
                    [CreateComponentHitData(1)])])
        };
        
        // Pre-assert: component has already exploded
        explodableComponent.HasExploded.ShouldBeTrue();
        targetPart.CurrentArmor.ShouldBe(10);
        
        // Act
        unit.ApplyDamage(hitLocations);
        
        // Assert
        // Verify that only the initial damage was applied, without explosion damage
        targetPart.CurrentArmor.ShouldBe(7); // 10 - 3 = 7
    }
    
    [Fact]
    public void ApplyDamage_WithNonExplodableComponent_ShouldNotAddExplosionDamage()
    {
        // Arrange
        var unit = CreateTestUnit();
        var targetPart = unit.Parts.First(p => p.Location == PartLocation.LeftArm);
        
        // Create a non-explodable component
        var nonExplodableComponent = new TestComponent("Non-Explodable Component", 3);
        targetPart.TryAddComponent(nonExplodableComponent, [1]);
        
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.LeftArm, 4, [new DiceResult(3)],
                [new LocationCriticalHitsData(PartLocation.LeftArm, 0, 1, 
                    [CreateComponentHitData(1)])])
        };
        
        targetPart.CurrentArmor.ShouldBe(10);
        
        // Act
        unit.ApplyDamage(hitLocations);
        
        // Assert
        // Verify that only the initial damage was applied, without explosion damage
        targetPart.CurrentArmor.ShouldBe(6); // 10 - 4 = 6
    }
    
    [Fact]
    public void ApplyDamage_WithZeroExplosionDamage_ShouldNotAddExplosionDamage()
    {
        // Arrange
        var unit = CreateTestUnit();
        var targetPart = unit.Parts.First(p => p.Location == PartLocation.LeftArm);
        
        // Create an explodable component with zero explosion damage
        var explodableComponent = new TestExplodableComponent("Zero Explosion Component", 0);
        targetPart.TryAddComponent(explodableComponent, [1]);
        
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.LeftArm, 6, [new DiceResult(3)],
                [new LocationCriticalHitsData(PartLocation.LeftArm, 0, 1, [CreateComponentHitData(1)])])
        };
        
        // Pre-assert: component has not exploded
        explodableComponent.HasExploded.ShouldBeFalse();
        targetPart.CurrentArmor.ShouldBe(10);
        
        // Act
        unit.ApplyDamage(hitLocations);
        
        // Assert
        explodableComponent.HasExploded.ShouldBeTrue();
        // Verify that only the initial damage was applied, without explosion damage
        targetPart.CurrentArmor.ShouldBe(4); // 10 - 6 = 4
    }
    
    [Fact]
    public void ApplyDamage_WithExplosion_ShouldAddExplosionEvent()
    {
        // Arrange
        var unit = CreateTestUnit();
        var targetPart = unit.Parts.First(p => p.Location == PartLocation.CenterTorso);
        
        // Create an explodable component
        var explodableComponent = Lrm5.CreateAmmo();
        
        // Add the component to the part
        targetPart.TryAddComponent(explodableComponent,[0]);
        
        // Create hit locations with critical hits that will trigger the explosion
        var criticalHitsData = new LocationCriticalHitsData(
            PartLocation.CenterTorso,
            10, // Roll value
            1,  // Number of critical hits
            [CreateComponentHitData(0)] // Hit the first slot where our component is
        );
        
        var hitLocations = new List<HitLocationData>
        {
            new(
                PartLocation.CenterTorso, // Location
                5, // Damage
                [], // Empty location roll
                [criticalHitsData] // Critical hits data
            )
        };
        
        // Act
        unit.ApplyDamage(hitLocations);
        
        // Assert
        // Dequeue all events and check for explosion event
        var foundExplosionEvent = false;
        while (unit.DequeueNotification() is { } uiEvent)
        {
            if (uiEvent.Type == UiEventType.Explosion && uiEvent.Parameters[0] == "LRM-5 Ammo")
            {
                foundExplosionEvent = true;
                break;
            }
        }
        
        foundExplosionEvent.ShouldBeTrue("Should have found an explosion event");
    }
    
    [Fact]
    public void ApplyDamage_WithUnitDestruction_ShouldAddUnitDestroyedEvent()
    {
        // Arrange
        var unit = CreateTestUnit();
        
        // Create hit locations that will destroy the unit
        var hitLocations = new List<HitLocationData>
        {
            new HitLocationData(
                PartLocation.CenterTorso, // Location
                100, // Damage enough to destroy the center torso completely
                new List<DiceResult>() // Empty location roll
            )
        };
        
        // Act
        unit.ApplyDamage(hitLocations);
        
        // Assert
        unit.Status.ShouldBe(UnitStatus.Destroyed);
        
        // Dequeue all events and check for unit destroyed event
        bool foundUnitDestroyedEvent = false;
        while (unit.DequeueNotification() is { } uiEvent)
        {
            if (uiEvent.Type == UiEventType.UnitDestroyed && uiEvent.Parameters[0] == unit.Name)
            {
                foundUnitDestroyedEvent = true;
                break;
            }
        }
        
        foundUnitDestroyedEvent.ShouldBeTrue("Should have found a unit destroyed event");
    }
    
    [Fact]
    public void AddEvent_ShouldAddEventToQueue()
    {
        // Arrange
        var sut = CreateTestUnit();
        var testEvent = new UiEvent(UiEventType.ArmorDamage, "TestLocation", "5");
        
        // Act
        sut.AddEvent(testEvent);
        
        // Assert
        sut.Notifications.Count.ShouldBe(1);
        sut.Events.Count.ShouldBe(1);
        var dequeuedEvent = sut.DequeueNotification();
        dequeuedEvent.ShouldNotBeNull();
        dequeuedEvent.Type.ShouldBe(UiEventType.ArmorDamage);
        dequeuedEvent.Parameters.Length.ShouldBe(2);
        dequeuedEvent.Parameters[0].ShouldBe("TestLocation");
        dequeuedEvent.Parameters[1].ShouldBe("5");
    }
    
    [Fact]
    public void DequeueNotification_ShouldReturnEventsInOrder()
    {
        // Arrange
        var sut = CreateTestUnit();
        var event1 = new UiEvent(UiEventType.ArmorDamage, "Location1", "5");
        var event2 = new UiEvent(UiEventType.StructureDamage, "Location2", "3");
        var event3 = new UiEvent(UiEventType.CriticalHit, "Component");
        
        // Act
        sut.AddEvent(event1);
        sut.AddEvent(event2);
        sut.AddEvent(event3);
        
        var dequeuedEvent1 = sut.DequeueNotification();
        var dequeuedEvent2 = sut.DequeueNotification();
        var dequeuedEvent3 = sut.DequeueNotification();
        var dequeuedEvent4 = sut.DequeueNotification(); // Should be null
        
        // Assert
        dequeuedEvent1.ShouldNotBeNull();
        dequeuedEvent1.Type.ShouldBe(UiEventType.ArmorDamage);
        
        dequeuedEvent2.ShouldNotBeNull();
        dequeuedEvent2.Type.ShouldBe(UiEventType.StructureDamage);
        
        dequeuedEvent3.ShouldNotBeNull();
        dequeuedEvent3.Type.ShouldBe(UiEventType.CriticalHit);
        
        dequeuedEvent4.ShouldBeNull();
    }
    
    [Fact]
    public void DequeueNotification_ShouldRemoveNotifications_ButKeepEvents()
    {
        // Arrange
        var sut = CreateTestUnit();
        var event1 = new UiEvent(UiEventType.ArmorDamage, "Location1", "5");
        
        // Act
        sut.AddEvent(event1);

        sut.DequeueNotification();

        // Assert
        sut.Notifications.Count.ShouldBe(0);
        sut.Events.Count.ShouldBe(1);
    }

    [Fact]
    public void ClearEvents_ShouldRemoveAllEvents()
    {
        // Arrange
        var sut = CreateTestUnit();
        sut.AddEvent(new UiEvent(UiEventType.ArmorDamage, "Location", "5"));
        sut.AddEvent(new UiEvent(UiEventType.StructureDamage, "Location", "3"));
        
        // Act
        sut.ClearEvents();
        var dequeuedEvent = sut.DequeueNotification();
        
        // Assert
        sut.Notifications.Count.ShouldBe(0);
        sut.Events.Count.ShouldBe(0);
        dequeuedEvent.ShouldBeNull();
    }

    [Fact]
    public void ResetTurnState_ShouldClearEvents()
    {
        // Arrange
        var sut = CreateTestUnit();
        sut.AddEvent(new UiEvent(UiEventType.ArmorDamage, "Location", "5"));
        sut.AddEvent(new UiEvent(UiEventType.StructureDamage, "Location", "3"));
        
        // Act
        sut.ResetTurnState();
        
        // Assert
        sut.Notifications.Count.ShouldBe(0);
        sut.Events.Count.ShouldBe(0);
    }
    
    [Fact]
    public void ApplyHeat_ShouldNotAddHeatPenaltiesForBaseUnit()
    {
        // Arrange
        var sut = CreateTestUnit();

        // Set initial heat to 15 (3 MP penalty)
        sut.ApplyHeat(new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = [new WeaponHeatData
                {
                    WeaponName = "TestWeapon",
                    HeatPoints = 15
                }
            ],
            DissipationData = default
        });

        // Assert
        // Verify no heat penalty for base unit
        sut.CurrentHeat.ShouldBe(15);
        sut.MovementHeatPenalty.ShouldBe(0);
        sut.AttackHeatPenalty.ShouldBe(0);
    }

    [Fact]
    private void EngineHeatSinks_ShouldBeZero_ByDefault()
    {
        var sut = CreateTestUnit();
        
        sut.EngineHeatSinks.ShouldBe(0);
    }
    
    [Fact]
    public void EngineHeatPenalty_ReturnsZero_ForBaseUnit()
    {
        // Arrange
        var sut = CreateTestUnit();
        
        // Act
        var engineHeatPenalty = sut.EngineHeatPenalty;
        
        // Assert
        engineHeatPenalty.ShouldBe(0);
    }

    [Fact]
    public void TotalPhaseDamage_InitiallyZero()
    {
        // Arrange
        var sut = CreateTestUnit();

        // Act & Assert
        sut.TotalPhaseDamage.ShouldBe(0);
    }

    [Fact]
    public void ApplyDamage_ShouldIncrementTotalPhaseDamage()
    {
        // Arrange
        var sut = CreateTestUnit();
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.CenterTorso, 5, []),
            new(PartLocation.LeftArm, 3, [])
        };

        // Act
        sut.ApplyDamage(hitLocations);

        // Assert
        sut.TotalPhaseDamage.ShouldBe(8); // 5 + 3
    }

    [Fact]
    public void ApplyDamage_WithMultipleCalls_ShouldAccumulateTotalPhaseDamage()
    {
        // Arrange
        var sut = CreateTestUnit();
        var firstHitLocations = new List<HitLocationData>
        {
            new(PartLocation.CenterTorso, 4, [])
        };
        var secondHitLocations = new List<HitLocationData>
        {
            new(PartLocation.LeftArm, 6, [])
        };

        // Act
        sut.ApplyDamage(firstHitLocations);
        sut.ApplyDamage(secondHitLocations);

        // Assert
        sut.TotalPhaseDamage.ShouldBe(10); // 4 + 6
    }

    [Fact]
    public void ApplyDamage_WithExplosionDamage_ShouldIncludeBothInTotalPhaseDamage()
    {
        // Arrange
        var sut = CreateTestUnit();
        var targetPart = sut.Parts.First(p => p.Location == PartLocation.LeftArm);

        // Create an explodable component
        var explodableComponent = new TestExplodableComponent("Explodable Component", 5);
        targetPart.TryAddComponent(explodableComponent, [1]);

        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.LeftArm, 3, [new DiceResult(3)],
                [new LocationCriticalHitsData(PartLocation.LeftArm, 0, 1,
                    [CreateComponentHitData(1)])])
        };

        // Act
        sut.ApplyDamage(hitLocations);

        // Assert
        sut.TotalPhaseDamage.ShouldBe(8); // 3 initial damage + 5 explosion damage
    }

    [Fact]
    public void ResetPhase_ShouldResetTotalPhaseDamageToZero()
    {
        // Arrange
        var sut = CreateTestUnit();
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.CenterTorso, 7, []),
            new(PartLocation.LeftArm, 4, [])
        };

        // Apply damage to accumulate TotalPhaseDamage
        sut.ApplyDamage(hitLocations);
        sut.TotalPhaseDamage.ShouldBe(11); // Verify damage was accumulated

        // Act
        sut.ResetPhaseState();

        // Assert
        sut.TotalPhaseDamage.ShouldBe(0);
    }

    // Helper class for testing explodable components
    private class TestExplodableComponent(string name, int explosionDamage, int size = 1) : TestComponent(name, size)
    {
        public override bool CanExplode => true;
        
        public override int GetExplosionDamage() => explosionDamage;
        
        public override void Hit()
        {
            base.Hit();
            HasExploded = true;
        }
    }
    
    // Helper class for testing generic methods
    private class TestDerivedComponent(string name, int size = 1) : TestComponent(name, size);
    private ComponentHitData CreateComponentHitData(int slot)
    {
        return new ComponentHitData
        {
            Slot = slot,
            Type = MakaMekComponent.ISAmmoMG
        };
    }

    [Fact]
    public void CanJump_BaseUnitClass_ShouldReturnFalse()
    {
        // Arrange
        var sut = CreateTestUnit();

        // Act & Assert
        sut.CanJump.ShouldBeFalse("Base Unit class should not be able to jump by default");
    }
}
