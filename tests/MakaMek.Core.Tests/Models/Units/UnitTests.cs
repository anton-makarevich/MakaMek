using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Events;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units;

public class UnitTests
{
    private readonly IRulesProvider _rulesProvider = Substitute.For<IRulesProvider>();

    private class OptionsUnitPart(string name, PartLocation location, IReadOnlyList<WeaponConfigurationOptions> options)
        : UnitPart(name, location, 1, 1, 1)
    {
        internal override bool CanBeBlownOff => true;

        public override IReadOnlyList<WeaponConfigurationOptions> GetWeaponsConfigurationOptions(HexPosition? forwardPosition = null)
        {
            return options;
        }
    }
    
    private class TestComponent(string name, int size = 1) : Component(new EquipmentDefinition(
        name,
        MakaMekComponent.Masc,
        0,
        size));

    private class TestWeapon(string name, int size =1, WeaponType type = WeaponType.Energy, MakaMekComponent? ammoType = null)
        : Weapon(new WeaponDefinition(
            name, 5, 3,
            0, 3, 6, 9,
            type, 10, 1, 1, size, 1, MakaMekComponent.MachineGun, ammoType));
    
    private class TestUnitPart(string name, PartLocation location, int maxArmor, int maxStructure, int slots)
        : UnitPart(name, location, maxArmor, maxStructure, slots)
    {
        internal override bool CanBeBlownOff => true;

        public override IReadOnlyList<WeaponConfigurationOptions> GetWeaponsConfigurationOptions(HexPosition? forwardPosition = null)
        {
            return [];
        }
    }

    public class TestUnit(
        string chassis,
        string model,
        int tonnage,
        IEnumerable<UnitPart> parts,
        Guid? id = null)
        : Unit(chassis, model, tonnage, parts, id)
    {
        public override int CalculateBattleValue() => 0;

        public override bool CanMoveBackward(MovementType type) => true;

        public override PartLocation? GetTransferLocation(PartLocation location) =>
            location==PartLocation.CenterTorso
                ?null
                : PartLocation.CenterTorso;

        public override LocationCriticalHitsData CalculateCriticalHitsData(PartLocation location,
            IDiceRoller diceRoller,
            IDamageTransferCalculator damageTransferCalculator) => throw new NotImplementedException();
        
        protected override void ApplyHeatEffects()
        {
        }

        public override void ApplyWeaponConfiguration(WeaponConfiguration config)
        {
        }

        public override void UpdateDestroyedStatus()
        {
            // Implement the same logic as Mech for testing
            var head = Parts.Values.FirstOrDefault(p => p.Location == PartLocation.Head);
            if (head is { IsDestroyed: true })
            {
                Status = UnitStatus.Destroyed;
                return;
            }
            var centerTorso = Parts.Values.FirstOrDefault(p => p.Location == PartLocation.CenterTorso);
            if (centerTorso is { IsDestroyed: true })
            {
                Status = UnitStatus.Destroyed;
            }
        }

        /// <summary>
        /// Helper method for testing - provides access to set the Status field
        /// </summary>
        public void SetStatusForTesting(UnitStatus status)
        {
            Status = status;
        }
    }
    
    public static TestUnit CreateTestUnit(Guid? id = null, int walkMp = 4)
    {
        var tonnage = 20;
        var engineData = new ComponentData
        {
            Type = MakaMekComponent.Engine,
            Assignments =
            [
                new LocationSlotAssignment(PartLocation.CenterTorso, 0, 3),
                new LocationSlotAssignment(PartLocation.CenterTorso, 7, 3)
            ],
            SpecificData = new EngineStateData(EngineType.Fusion, walkMp*tonnage)
        };
        var centerTorso = new TestUnitPart("Center Torso", PartLocation.CenterTorso, 10, 5, 10);
        centerTorso.TryAddComponent(new Engine(engineData), [0, 1, 2, 7, 8, 9]).ShouldBeTrue();
        
        var parts = new List<UnitPart>
        {
            new TestUnitPart("Head", PartLocation.Head, 9, 3, 6),
            centerTorso,
            new TestUnitPart("Left Arm", PartLocation.LeftArm, 10, 5, 10),
            new TestUnitPart("Right Arm", PartLocation.RightArm, 10, 5, 10)
        };

        return new TestUnit("Test", "Unit", tonnage, parts, id);
    }

    [Fact]
    public void GetWeaponsConfigurationOptions_AggregatesAndDeduplicatesByType()
    {
        var part1 = new OptionsUnitPart(
            "P1",
            PartLocation.LeftArm,
            [new WeaponConfigurationOptions(WeaponConfigurationType.TorsoRotation, [HexDirection.TopRight])]);

        var part2 = new OptionsUnitPart(
            "P2",
            PartLocation.RightArm,
            [new WeaponConfigurationOptions(WeaponConfigurationType.TorsoRotation, [HexDirection.TopLeft])]);

        var part3 = new OptionsUnitPart(
            "P3",
            PartLocation.CenterTorso,
            [new WeaponConfigurationOptions(WeaponConfigurationType.ArmsFlip, [HexDirection.Top, HexDirection.Bottom])]);

        var unit = new TestUnit("Test", "Unit", 20, [part1, part2, part3]);

        var options = unit.GetWeaponsConfigurationOptions();

        options.Count.ShouldBe(2);
        options[0].Type.ShouldBe(WeaponConfigurationType.TorsoRotation);
        options[0].AvailableDirections.ShouldBe([HexDirection.TopRight, HexDirection.TopLeft]);
        options[1].Type.ShouldBe(WeaponConfigurationType.ArmsFlip);
        options[1].AvailableDirections.ShouldBe([HexDirection.Top, HexDirection.Bottom]);
    }
    
    private void MountWeaponOnUnit(TestUnit unit, TestWeapon weapon, PartLocation location, int[] slots)
    {
        var part = unit.Parts[location];
        part.TryAddComponent(weapon,slots);
    }
    
    [Fact]
    public void GetComponentsAtLocation_ShouldReturnComponentsAtSpecifiedLocation()
    {
        // Arrange
        var leftArmPart = new TestUnitPart("Left Arm", PartLocation.LeftArm, 10, 5, 10);
        var rightArmPart = new TestUnitPart("Right Arm", PartLocation.RightArm, 10, 5, 10);
        var testUnit = new TestUnit("Test", "Unit", 20, [leftArmPart, rightArmPart]);
        
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
        var testUnit = new TestUnit("Test", "Unit", 20, [leftArmPart]);
        
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
        
        var leftArmComponent = new TestComponent("Left Arm Component", 2);
        var rightArmComponent = new TestComponent("Right Arm Component", 2);
        var unmountedComponent = new TestComponent("Unmounted Component", 2);
        
        leftArmPart.TryAddComponent(leftArmComponent);
        rightArmPart.TryAddComponent(rightArmComponent);
        
        // Act
        var leftArmComponentPart = leftArmComponent.FirstMountPart;
        var rightArmComponentPart = rightArmComponent.FirstMountPart;
        var unmountedComponentPart = unmountedComponent.FirstMountPart;
        
        // Assert
        leftArmComponentPart.ShouldBe(leftArmPart);
        rightArmComponentPart.ShouldBe(rightArmPart);
        unmountedComponentPart.ShouldBeNull();
    }

    [Fact]
    public void GetMountedComponentAtLocation_ShouldReturnComponentAtSpecificSlot()
    {
        // Arrange
        var unit = CreateTestUnit();
        var weapon1 = new TestWeapon("Weapon 1", 2);
        var weapon2 = new TestWeapon("Weapon 2", 2);

        MountWeaponOnUnit(unit, weapon1, PartLocation.LeftArm, [0, 1]);
        MountWeaponOnUnit(unit, weapon2, PartLocation.LeftArm, [2, 3]);

        // Act
        var foundWeapon1 = unit.GetMountedComponentAtLocation<Weapon>(PartLocation.LeftArm, 0);
        var foundWeapon2 = unit.GetMountedComponentAtLocation<Weapon>(PartLocation.LeftArm, 2);
        var notFoundWeapon = unit.GetMountedComponentAtLocation<Weapon>(PartLocation.LeftArm, 4);

        // Assert
        foundWeapon1.ShouldNotBeNull();
        foundWeapon1.ShouldBe(weapon1);

        foundWeapon2.ShouldNotBeNull();
        foundWeapon2.ShouldBe(weapon2);

        notFoundWeapon.ShouldBeNull();
    }

    [Fact]
    public void GetMountedComponentAtLocation_ShouldReturnNull_WhenSlotDoesNotExist()
    {
        // Arrange
        var unit = CreateTestUnit();
        var weapon = new TestWeapon("Weapon", 2);
        MountWeaponOnUnit(unit, weapon, PartLocation.LeftArm, [0, 1]);

        // Act
        var result = unit.GetMountedComponentAtLocation<Weapon>(PartLocation.LeftArm, -1);

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
                Weapon = new ComponentData
                {
                    Name = "Test Weapon",
                    Type = MakaMekComponent.ISAmmoMG,
                    Assignments = [
                        new LocationSlotAssignment(PartLocation.LeftArm, 0, 2)
                    ]
                },
                TargetId = targetUnit.Id,
                IsPrimaryTarget = true
            }
        };
        
        // Act
        var  act = () => unit.DeclareWeaponAttack(weaponTargets);
        
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
        
        var weapon = new TestWeapon("Test Weapon", 2);
        MountWeaponOnUnit(attacker, weapon, PartLocation.LeftArm, [0, 1]);
        
        attacker.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        target.Deploy(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top));
        
        var weaponTargets = new List<WeaponTargetData>
        {
            new()
            {
                Weapon = new ComponentData
                {
                    Name = "Test Weapon",
                    Type = MakaMekComponent.MachineGun,
                    Assignments = [
                        new LocationSlotAssignment(PartLocation.LeftArm, 0, 2)
                    ]
                },
                TargetId = targetId,
                IsPrimaryTarget = true
            }
        };
        
        // Act
        attacker.DeclareWeaponAttack(weaponTargets);
        
        // Assert
        var weaponTargetData = attacker.DeclaredWeaponTargets
            ?.FirstOrDefault(wt =>
            {
                var primaryAssignment = wt.Weapon.Assignments.FirstOrDefault();
                return primaryAssignment != null &&
                       primaryAssignment.Location == PartLocation.LeftArm &&
                       primaryAssignment.GetSlots().OrderBy(s => s).SequenceEqual(new[] { 0, 1 }.OrderBy(s => s));
            });
        weaponTargetData.ShouldNotBeNull();
        weaponTargetData.TargetId.ShouldBe(targetId);
        weaponTargetData.IsPrimaryTarget.ShouldBeTrue();
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
        
        var weapon1 = new TestWeapon("Weapon 1", 2);
        var weapon2 = new TestWeapon("Weapon 2", 2);
        
        MountWeaponOnUnit(attacker, weapon1, PartLocation.LeftArm, [0, 1]);
        MountWeaponOnUnit(attacker, weapon2, PartLocation.RightArm, [2, 3]);
        
        attacker.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        target1.Deploy(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top));
        target2.Deploy(new HexPosition(new HexCoordinates(1, 3), HexDirection.Top));
        
        var weaponTargets = new List<WeaponTargetData>
        {
            new()
            {
                Weapon = new ComponentData
                {
                    Name = "Weapon 1",
                    Type = MakaMekComponent.MachineGun,
                    Assignments = [
                        new LocationSlotAssignment(PartLocation.LeftArm, 0, 2)
                    ]
                },
                TargetId = targetId1,
                IsPrimaryTarget = true
            },
            new()
            {
                Weapon = new ComponentData
                {
                    Name = "Weapon 2",
                    Assignments = [
                        new LocationSlotAssignment(PartLocation.RightArm, 2, 2)
                    ],
                    Type = MakaMekComponent.MachineGun
                },
                TargetId = targetId2,
                IsPrimaryTarget = true
            }
        };
        
        // Act
        attacker.DeclareWeaponAttack(weaponTargets);
        
        // Assert
        var weapon1Target = attacker.DeclaredWeaponTargets
            ?.FirstOrDefault(wt =>
            {
                var primaryAssignment = wt.Weapon.Assignments.FirstOrDefault();
                return primaryAssignment != null &&
                       primaryAssignment.Location == PartLocation.LeftArm &&
                       primaryAssignment.GetSlots().OrderBy(s => s).SequenceEqual(new[] { 0, 1 }.OrderBy(s => s));
            });
        weapon1Target.ShouldNotBeNull();
        weapon1Target.TargetId.ShouldBe(targetId1);
        weapon1Target.IsPrimaryTarget.ShouldBeTrue();

        var weapon2Target = attacker.DeclaredWeaponTargets
            ?.FirstOrDefault(wt =>
            {
                var primaryAssignment = wt.Weapon.Assignments.FirstOrDefault();
                return primaryAssignment != null &&
                       primaryAssignment.Location == PartLocation.RightArm &&
                       primaryAssignment.GetSlots().OrderBy(s => s).SequenceEqual(new[] { 2, 3 }.OrderBy(s => s));
            });
        weapon2Target.ShouldNotBeNull();
        weapon2Target.TargetId.ShouldBe(targetId2);
        weapon2Target.IsPrimaryTarget.ShouldBeTrue();

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
        
        var weapon = new TestWeapon("Test Weapon", 2);
        MountWeaponOnUnit(attacker, weapon, PartLocation.LeftArm, [0, 1]);
        
        attacker.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        target.Deploy(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top));
        
        var weaponTargets = new List<WeaponTargetData>
        {
            new()
            {
                Weapon = new ComponentData
                {
                    Name = "Test Weapon",
                    Type = MakaMekComponent.MachineGun,
                    Assignments = [
                        new LocationSlotAssignment(PartLocation.LeftArm, 0, 2)
                    ]
                },
                TargetId = targetId,
                IsPrimaryTarget = true
            },
            new()
            {
                Weapon = new ComponentData
                {
                    Name = "Non-existent Weapon",
                    Type = MakaMekComponent.MachineGun,
                    Assignments = [
                        new LocationSlotAssignment(PartLocation.RightArm, 4, 2)
                    ]
                },
                TargetId = targetId,
                IsPrimaryTarget = false
            }
        };
        
        // Act
        attacker.DeclareWeaponAttack(weaponTargets);
        
        // Assert
        var weaponTarget = attacker.DeclaredWeaponTargets
            ?.FirstOrDefault(wt =>
            {
                var primaryAssignment = wt.Weapon.Assignments.FirstOrDefault();
                return primaryAssignment != null &&
                       primaryAssignment.Location == PartLocation.LeftArm &&
                       primaryAssignment.GetSlots().OrderBy(s => s).SequenceEqual(new[] { 0, 1 }.OrderBy(s => s));
            });
        weaponTarget.ShouldNotBeNull();
        weaponTarget.TargetId.ShouldBe(targetId);
        weaponTarget.IsPrimaryTarget.ShouldBeTrue();
        attacker.HasDeclaredWeaponAttack.ShouldBeTrue();
    }
    
    [Fact]
    public void GetComponentsAtLocation_ReturnsEmptyCollection_WhenNoComponentsAtLocation()
    {
        // Arrange
        var testUnit = CreateTestUnit();

        // Act
        var components = testUnit.GetComponentsAtLocation(PartLocation.Head);

        // Assert
        components.ShouldBeEmpty();
    }
    
        [Fact]
    public void HasAmmo_ShouldReturnFalse_WhenNoWeapons()
    {
        // Arrange
        var sut = CreateTestUnit();

        // Act & Assert
        sut.HasAmmo.ShouldBeFalse();
    }
    
    [Fact]
    public void HasAmmo_ShouldReturnFalse_WhenOnlyEnergyWeapons()
    {
        // Arrange
        var sut = CreateTestUnit();
        var rightArm = sut.Parts[PartLocation.RightArm];
        rightArm.TryAddComponent(new MediumLaser()).ShouldBeTrue();
        
        // Act & Assert
        sut.HasAmmo.ShouldBeFalse();
    }
    
    [Fact]
    public void HasAmmo_ShouldReturnTrue_WhenAmmoWeaponHasAmmo()
    {
        // Arrange
        var sut = CreateTestUnit();
        var rightArm = sut.Parts[PartLocation.RightArm];
        var lrm15 = new Lrm15();
        rightArm.TryAddComponent(lrm15).ShouldBeTrue();
        
        var leftArm = sut.Parts[PartLocation.LeftArm];
        var ammo = Lrm15.CreateAmmo();
        leftArm.TryAddComponent(ammo);
        
        // Act & Assert
        sut.HasAmmo.ShouldBeTrue();
        ammo.RemainingShots.ShouldBeGreaterThan(0);
    }
    
    [Fact]
    public void HasAmmo_ShouldReturnFalse_WhenAmmoWeaponHasNoAmmo()
    {
        // Arrange
        var sut = CreateTestUnit();
        var rightArm = sut.Parts[PartLocation.RightArm];
        var lrm15 = new Lrm15();
        rightArm.TryAddComponent(lrm15).ShouldBeTrue();
        
        var leftArm = sut.Parts[PartLocation.LeftArm];
        var ammo = Lrm15.CreateAmmo();
        leftArm.TryAddComponent(ammo);
    
        // Deplete all ammo
        while (ammo.RemainingShots > 0)
        {
            ammo.UseShot();
        }
    
        // Act & Assert
        sut.HasAmmo.ShouldBeFalse();
        ammo.RemainingShots.ShouldBe(0);
    }
    
    [Fact]
    public void GetAmmoForWeapon_ReturnsEmptyCollection_WhenWeaponDoesNotRequireAmmo()
    {
        // Arrange
        var testUnit = CreateTestUnit();
        var energyWeapon = new TestWeapon("Energy Weapon", 2);
        
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
        var testUnit = new TestUnit("Test", "Unit", 20, [centerTorso, leftTorso]);
        
        var ac5Weapon = new TestWeapon("AC/5", 2, WeaponType.Ballistic, MakaMekComponent.ISAmmoAC5);
        var ac5Ammo1 = AmmoTests.CreateAmmo(Ac5.Definition, 20);
        var ac5Ammo2 = AmmoTests.CreateAmmo(Ac5.Definition, 20);
        var lrm5Ammo = AmmoTests.CreateAmmo(Lrm5.Definition, 24);
        
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
        var energyWeapon = new TestWeapon("Energy Weapon", 2);
        
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
        var testUnit = new TestUnit("Test", "Unit", 20, [centerTorso, leftTorso]);
        
        var ac5Weapon = new TestWeapon("AC/5", 2, WeaponType.Ballistic, MakaMekComponent.ISAmmoAC5);
        var ac5Ammo1 = AmmoTests.CreateAmmo(Ac5.Definition, 20);
        var ac5Ammo2 = AmmoTests.CreateAmmo(Ac5.Definition, 15);
        
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
        var testUnit = new TestUnit("Test", "Unit", 20, [centerTorso]);
        
        var ac5Weapon = new TestWeapon("AC/5", 2, WeaponType.Ballistic, MakaMekComponent.ISAmmoAC5);
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
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(PartLocation.CenterTorso,5), // No aimed shot, no location roll
            CreateHitDataForLocation(PartLocation.LeftArm, 3) // No aimed shot, no location roll
        };
        
        // Get initial armor values
        var centerTorsoPart = unit.Parts[PartLocation.CenterTorso];
        var leftArmPart = unit.Parts[PartLocation.LeftArm];
        var initialCenterTorsoArmor = centerTorsoPart.CurrentArmor;
        var initialLeftArmArmor = leftArmPart.CurrentArmor;
        
        // Act
        unit.ApplyDamage(hitLocations, HitDirection.Front);
        
        // Assert
        centerTorsoPart.CurrentArmor.ShouldBe(initialCenterTorsoArmor - 5);
        leftArmPart.CurrentArmor.ShouldBe(initialLeftArmArmor - 3);
    }
    
    [Fact]
    public void ApplyDamage_WithHitLocationsList_ShouldIgnoreNonExistentParts()
    {
        // Arrange
        var unit = CreateTestUnit();
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(PartLocation.CenterTorso, 5), // No aimed shot, no location roll
            CreateHitDataForLocation(PartLocation.LeftLeg, 3) // Unit doesn't have a LeftLeg part, no aimed shot, no location roll
        };
        
        // Get initial armor values
        var centerTorsoPart = unit.Parts[PartLocation.CenterTorso];
        var initialCenterTorsoArmor = centerTorsoPart.CurrentArmor;
        
        // Act
        unit.ApplyDamage(hitLocations, HitDirection.Front);
        
        // Assert
        centerTorsoPart.CurrentArmor.ShouldBe(initialCenterTorsoArmor - 5);
        // No exception should be thrown for the non-existent part
    }
    
    [Fact]
    public void ApplyDamage_WithEmptyHitLocationsList_ShouldNotChangeArmor()
    {
        // Arrange
        var unit = CreateTestUnit();
        var hitLocations = new List<LocationHitData>();
        
        // Get initial armor values for all parts
        var initialArmorValues = unit.Parts.Values.ToDictionary(p => p.Location, p => p.CurrentArmor);
        
        // Act
        unit.ApplyDamage(hitLocations, HitDirection.Front);
        
        // Assert
        foreach (var part in unit.Parts.Values)
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
        
        var unit = new TestUnit("Test", "Unit", 20, parts);
        
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
        
        var unit = new TestUnit("Test", "Unit", 20, parts);
        
        // Apply damage to reduce armor
        unit.ApplyDamage([CreateHitDataForLocation(parts[0].Location,5)], HitDirection.Front); // Center Torso: 10 -> 5
        unit.ApplyDamage([CreateHitDataForLocation(parts[1].Location,10)], HitDirection.Front); // Left Arm: 15 -> 5
        
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
        
        var unit = new TestUnit("Test", "Unit", 20, parts);
        
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
        
        var unit = new TestUnit("Test", "Unit", 20, parts);
        
        // Apply damage to reduce armor and structure
        unit.ApplyDamage([CreateHitDataForLocation(parts[0].Location, 15)], HitDirection.Front); 
        unit.ApplyDamage([CreateHitDataForLocation(parts[1].Location, 20)], HitDirection.Front); 
        
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
        
        var unit = new TestUnit("Test", "Unit", 20, parts);
        
        // Initial values
        unit.TotalMaxArmor.ShouldBe(45); // 10 + 15 + 20
        unit.TotalCurrentArmor.ShouldBe(45);
        unit.TotalMaxStructure.ShouldBe(25); // 5 + 8 + 12
        unit.TotalCurrentStructure.ShouldBe(25);
        
        // Act - Apply damage to one part
        unit.ApplyDamage([CreateHitDataForLocation(parts[0].Location, 5)], HitDirection.Front); // Reduce Center Torso armor by 5
        
        // Assert - Check updated values
        unit.TotalCurrentArmor.ShouldBe(40); // 5 + 15 + 20
        unit.TotalCurrentStructure.ShouldBe(25); // Structure unchanged
        
        // Act - Apply more damage to penetrate armor and damage structure
        unit.ApplyDamage([CreateHitDataForLocation(parts[0].Location, 8)], HitDirection.Front); // Reduce remaining CT armor (5) and damage structure (3)
        
        // Assert - Check updated values
        unit.TotalCurrentArmor.ShouldBe(35); // 0 + 15 + 20
        unit.TotalCurrentStructure.ShouldBe(22); // 2 + 8 + 12
    }
    
    [Fact]
    public void FireWeapon_UseAmmo_ForBallisticWeapon()
    {
        // Arrange
        var unit = CreateTestUnit();
        var ballisticWeapon = new TestWeapon("Ballistic Weapon", 2, WeaponType.Ballistic, MakaMekComponent.ISAmmoAC5);
        MountWeaponOnUnit(unit, ballisticWeapon, PartLocation.LeftArm, [0, 1]);
        
        // Add ammo to the unit
        var ammo = AmmoTests.CreateAmmo(Ac5.Definition, 10);
        var rightArmPart = unit.Parts[PartLocation.RightArm];
        rightArmPart.TryAddComponent(ammo);
        
        var weaponData = new ComponentData
        {
            Name = ballisticWeapon.Name,
            Type = MakaMekComponent.AC5,
            Assignments = [new LocationSlotAssignment(PartLocation.LeftArm, 0, 2)]
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
        
        var weaponData = new ComponentData
        {
            Name = "Non-existent Weapon",
            Type = MakaMekComponent.MachineGun,
            Assignments = [new LocationSlotAssignment(PartLocation.LeftArm, 0, 2)]
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
        var weapon = new TestWeapon("Test Weapon", 2);
        MountWeaponOnUnit(unit, weapon, PartLocation.LeftArm, [0, 1]);
        
        // Destroy the weapon
        weapon.Hit();
        
        var weaponData = new ComponentData
        {
            Name = weapon.Name,
            Type = MakaMekComponent.MachineGun,
            Assignments = [new LocationSlotAssignment(PartLocation.LeftArm, 0, 2)]
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
        var ballisticWeapon = new TestWeapon("Ballistic Weapon", 2, WeaponType.Ballistic, MakaMekComponent.ISAmmoAC5);
        MountWeaponOnUnit(unit, ballisticWeapon, PartLocation.LeftArm, [0, 1]);
        
        // Add multiple ammo components with different shot counts
        var ammo1 = AmmoTests.CreateAmmo(Ac5.Definition, 3);
        var ammo2 = AmmoTests.CreateAmmo(Ac5.Definition, 8); // This one has more shots
        var ammo3 = AmmoTests.CreateAmmo(Ac5.Definition, 5);
        
        var rightArmPart = unit.Parts[PartLocation.RightArm];
        rightArmPart.TryAddComponent(ammo1);
        rightArmPart.TryAddComponent(ammo2);
        rightArmPart.TryAddComponent(ammo3);
        
        var weaponData = new ComponentData
        {
            Name = ballisticWeapon.Name,
            Type = MakaMekComponent.AC5,
            Assignments = [new LocationSlotAssignment(PartLocation.LeftArm, 0, 2)]
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
        
        // Move the unit with the Run movement type
        unit.Move(new MovementPath([
            new PathSegment(deployPosition, deployPosition, 5)], MovementType.Run));
        
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
        unit.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        
        // Add a weapon to the unit
        var weapon = new TestWeapon("Test Laser");
        MountWeaponOnUnit(unit, weapon, PartLocation.RightArm,[3]);
        
        // Set the weapon's target
        unit.DeclareWeaponAttack([
            new WeaponTargetData
            {
                Weapon = new ComponentData
                {
                    Name = "Test Laser",
                    Type = MakaMekComponent.ISAmmoMG,
                    Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 3, 1)]
                },
                TargetId = targetUnit.Id,
                IsPrimaryTarget = true
            }
        ]);
        
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
        
        // Deploy and move the unit
        var deployPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        unit.Deploy(deployPosition);
        
                // Add a weapon to the unit
        var weapon = new TestWeapon("Test Laser");
        MountWeaponOnUnit(unit, weapon, PartLocation.RightArm,[3]);
        
        // Set the weapon's target
        unit.DeclareWeaponAttack([
            new WeaponTargetData
            {
                Weapon = weapon.ToData(),
                TargetId = targetUnit.Id,
                IsPrimaryTarget = true
            }
        ]);

        // Move the unit with the Jump movement type
        unit.Move(new MovementPath([
            new PathSegment(deployPosition, deployPosition, 3)], MovementType.Jump));
        
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
        var rightArmPart = unit.Parts[PartLocation.RightArm];
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
    public void GetHeatData_WithHeatSinks_ReturnsDissipationDataConsideringActiveHeatSinksOnly()
    {
        // Arrange
        var unit = CreateTestUnit();
        var rulesProvider = Substitute.For<IRulesProvider>();
        
        // Add heat sinks to the unit
        var rightArmPart = unit.Parts[PartLocation.RightArm];
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
        var rightArmPart = unit.Parts[PartLocation.RightArm];
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
        var part = unit.Parts[PartLocation.LeftArm];
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
        var part = unit.Parts[PartLocation.LeftArm];
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
    public void ApplyCriticalHits_WithCockpitCriticalHit_KillsPilot()
    {
        // Arrange
        var head = new Head("Head", 10, 5);
        var pilot = new MechWarrior("John", "Doe");
        var unit = new TestUnit("Test", "Unit", 20, [head]);
        unit.AssignPilot(pilot);
        var cockpit = unit.GetAllComponents<Cockpit>().First();
        var hitLocation = new LocationCriticalHitsData(PartLocation.Head, [4, 4], 1,
                    [
                        new ComponentHitData
                        {
                            Slot = cockpit.MountedAtFirstLocationSlots[0],
                            Type = MakaMekComponent.Cockpit
                        }
                    ],
                    false);

        // Pre-assert: component is not destroyed
        cockpit.IsDestroyed.ShouldBeFalse();
        // Act
        unit.ApplyCriticalHits([hitLocation]);
        // Assert
        cockpit.IsDestroyed.ShouldBeTrue();
        pilot.IsDead.ShouldBeTrue();
    }
    
    [Fact]
    public void ApplyCriticalHits_ShouldBlowOffPart_WhenCriticalHitsBlownOffIsTrue()
    {
        // Arrange
        var unit = CreateTestUnit();
        var targetPart = unit.Parts[PartLocation.LeftArm];
        
        var hitLocations = new List<LocationCriticalHitsData>
        {
            new(PartLocation.LeftArm, [4, 4], 1,
                [],
                true)
        };
        
        // Act
        unit.ApplyCriticalHits(hitLocations);
        
        // Assert
        targetPart.IsBlownOff.ShouldBeTrue();
        targetPart.IsDestroyed.ShouldBeTrue();
    }
    
    [Fact]
    public void ApplyCriticalHits_ShouldApplyCriticalHits()
    {
        // Arrange
        var unit = CreateTestUnit();
        var targetPart = unit.Parts[PartLocation.LeftArm];
        var component = new TestComponent("Test Component", 3);
        targetPart.TryAddComponent(component);
        
        var hitLocations = new List<LocationCriticalHitsData>
        {
            new(PartLocation.LeftArm, [4, 4], 1,
                [
                    new ComponentHitData
                    {
                        Slot = component.MountedAtFirstLocationSlots[0],
                        Type = MakaMekComponent.MachineGun
                    }
                ],
                false)
        };
        
        // Act
        unit.ApplyCriticalHits(hitLocations);
        
        // Assert
        targetPart.HitSlots.Count.ShouldBe(1);
        component.IsDestroyed.ShouldBeTrue();
    }
    
    [Fact]
    public void ApplyCriticalHits_ShouldNotApplyCriticalHits_WhenCriticalHitsAreEmpty()
    {
        // Arrange
        var unit = CreateTestUnit();
        var targetPart = unit.Parts[PartLocation.LeftArm];
        var component = new TestComponent("Test Component", 3);
        targetPart.TryAddComponent(component);
        
        var hitLocations = new List<LocationCriticalHitsData>
        {
            new(PartLocation.LeftArm, [4, 4], 1,
                [],
                false)
        };
        
        // Act
        unit.ApplyCriticalHits(hitLocations);
        
        // Assert
        targetPart.HitSlots.Count.ShouldBe(0);
        component.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void ApplyCriticalHits_WithExplodableComponent_ShouldAddExplosionDamage_AndHitPilot()
    {
        // Arrange
        var unit = CreateTestUnit();
        var pilot = new MechWarrior("John", "Doe");
        unit.AssignPilot(pilot);
        var targetPart = unit.Parts[PartLocation.LeftArm];
        
        // Create an explodable component
        var explodableComponent = new TestExplodableComponent("Explodable Component", 3);
        targetPart.TryAddComponent(explodableComponent, [1]);
        
        var hitLocations = new List<LocationCriticalHitsData>
        {
            new(PartLocation.LeftArm, [4, 4], 1,
                [
                    new ComponentHitData
                    {
                        Slot = explodableComponent.MountedAtFirstLocationSlots[0],
                        Type = MakaMekComponent.MachineGun,
                        ExplosionDamage = 3,
                        ExplosionDamageDistribution = [
                            new LocationDamageData(PartLocation.LeftArm, 0, 3, false)
                        ]
                    }
                ],
                false) 
        };
        
        // Pre-assert: component has not exploded
        explodableComponent.HasExploded.ShouldBeFalse();
        targetPart.CurrentStructure.ShouldBe(5);
        var initialArmor = targetPart.CurrentArmor;
        
        // Act
        unit.ApplyCriticalHits(hitLocations);
        
        // Assert
        explodableComponent.HasExploded.ShouldBeTrue();
        targetPart.CurrentStructure.ShouldBe(2); // 5 - 3 = 2
        pilot.Injuries.ShouldBe(2);
        targetPart.CurrentArmor.ShouldBe(initialArmor); // explosion damage is not applied to armor
        
        // Dequeue all events and check for explosion event
        var foundExplosionEvent = false;
        while (unit.DequeueNotification() is { } uiEvent)
        {
            if (uiEvent.Type == UiEventType.Explosion)
            {
                foundExplosionEvent = true;
                break;
            }
        }
        
        foundExplosionEvent.ShouldBeTrue("Should have found an explosion event");
    }
    
    [Fact]
    public void ApplyCriticalHits_WithMultipleExplodableComponents_ShouldAddAllExplosionDamage()
    {
        // Arrange
        var unit = CreateTestUnit();
        var targetPart = unit.Parts[PartLocation.LeftArm];
        
        // Create multiple explodable components
        var explodableComponent1 = new TestExplodableComponent("Explodable Component 1", 3);
        var explodableComponent2 = new TestExplodableComponent("Explodable Component 2", 1);
        targetPart.TryAddComponent(explodableComponent1, [1]);
        targetPart.TryAddComponent(explodableComponent2, [2]);
        
        var hitLocations = new List<LocationCriticalHitsData>
        {
            new(PartLocation.LeftArm, [4, 4], 1,
                [
                    new ComponentHitData
                    {
                        Slot = explodableComponent1.MountedAtFirstLocationSlots[0],
                        Type = MakaMekComponent.MachineGun,
                        ExplosionDamage = 3,
                        ExplosionDamageDistribution = [
                            new LocationDamageData(PartLocation.LeftArm, 0, 3, false)
                        ]
                    },
                    new ComponentHitData
                    {
                        Slot = explodableComponent2.MountedAtFirstLocationSlots[0],
                        Type = MakaMekComponent.MachineGun,
                        ExplosionDamage = 1,
                        ExplosionDamageDistribution = [
                            new LocationDamageData(PartLocation.LeftArm, 0, 1, false)
                        ]
                    }
                ],
                false)
        };
        
        // Pre-assert: components have not exploded
        explodableComponent1.HasExploded.ShouldBeFalse();
        explodableComponent2.HasExploded.ShouldBeFalse();
        targetPart.CurrentStructure.ShouldBe(5);
        
        // Act
        unit.ApplyCriticalHits(hitLocations);
        
        // Assert
        explodableComponent1.HasExploded.ShouldBeTrue();
        explodableComponent2.HasExploded.ShouldBeTrue();
 
        targetPart.CurrentStructure.ShouldBe(1); // 5 - (3 + 1) = 1
    }
    
    [Fact]
    public void ApplyCriticalHits_WithAlreadyExplodedComponent_ShouldNotAddExplosionDamageAgain()
    {
        // Arrange
        var unit = CreateTestUnit();
        var targetPart = unit.Parts[PartLocation.LeftArm];
        
        // Create an explodable component that has already exploded
        var explodableComponent = new TestExplodableComponent("Explodable Component", 5);
        explodableComponent.Hit(); // This will set HasExploded to true
        targetPart.TryAddComponent(explodableComponent, [1]);
        
        var hitLocations = new List<LocationCriticalHitsData>
        {
            new(PartLocation.LeftArm, [4, 4], 1,
                [
                    new ComponentHitData
                    {
                        Slot = explodableComponent.MountedAtFirstLocationSlots[0],
                        Type = MakaMekComponent.MachineGun
                    }
                ],
                false)
        };
        
        // Pre-assert: component has already exploded
        explodableComponent.HasExploded.ShouldBeTrue();
        targetPart.CurrentStructure.ShouldBe(5);
        
        // Act
        unit.ApplyCriticalHits(hitLocations);
        
        // Assert
        targetPart.CurrentStructure.ShouldBe(5);
    }
    
    [Fact]
    public void ApplyCriticalHits_WithNonExplodableComponent_ShouldNotAddExplosionDamage()
    {
        // Arrange
        var unit = CreateTestUnit();
        var targetPart = unit.Parts[PartLocation.LeftArm];
        var component = new TestComponent("Test Component", 3);
        targetPart.TryAddComponent(component);

        var hitLocations = new List<LocationCriticalHitsData>
        {
            new(PartLocation.LeftArm, [4, 4], 1,
                [
                    new ComponentHitData
                    {
                        Slot = component.MountedAtFirstLocationSlots[0],
                        Type = MakaMekComponent.MachineGun
                    }
                ],
                false)
        };
        var initialStructure = targetPart.CurrentStructure;

        // Act
        unit.ApplyCriticalHits(hitLocations);

        // Assert
        targetPart.CurrentStructure.ShouldBe(initialStructure);
    }

    [Fact]
    public void ApplyCriticalHits_WithHeadBlownOff_ShouldDestroyUnit()
    {
        // Arrange - This tests lines 491-494 (unit destruction due to critical hits)
        var unit = CreateTestUnit();
        var initialDestroyedStatus = unit.IsDestroyed;
        var initialEventCount = unit.Events.Count;

        var hitLocations = new List<LocationCriticalHitsData>
        {
            new(PartLocation.Head, [6, 6], 0, null, true) // Head blown off
        };

        // Act
        unit.ApplyCriticalHits(hitLocations);

        // Assert
        initialDestroyedStatus.ShouldBeFalse(); // Unit was not destroyed initially
        unit.IsDestroyed.ShouldBeTrue(); // Unit should now be destroyed
        unit.Events.Count.ShouldBe(initialEventCount + 1); // Should have added destruction event
        unit.Events[^1].Type.ShouldBe(UiEventType.UnitDestroyed);
        unit.Events[^1].Parameters[0].ShouldBe(unit.Name);
    }

    [Fact]
    public void ApplyCriticalHits_WithCenterTorsoDestroyed_ShouldDestroyUnit()
    {
        // Arrange - This tests lines 491-494 (unit destruction due to critical hits)
        var unit = CreateTestUnit();
        var centerTorso = unit.Parts[PartLocation.CenterTorso];

        // Destroy the center torso by reducing structure to 0
        centerTorso.ApplyDamage(centerTorso.CurrentArmor + centerTorso.CurrentStructure, HitDirection.Front);

        var initialDestroyedStatus = unit.IsDestroyed;
        var initialEventCount = unit.Events.Count;

        var hitLocations = new List<LocationCriticalHitsData>
        {
            new(PartLocation.CenterTorso, [4, 4], 1,
                [
                    new ComponentHitData
                    {
                        Slot = 0,
                        Type = MakaMekComponent.Engine
                    }
                ],
                false)
        };

        // Act
        unit.ApplyCriticalHits(hitLocations);

        // Assert
        initialDestroyedStatus.ShouldBeFalse(); // Unit was not destroyed initially
        unit.IsDestroyed.ShouldBeTrue(); // Unit should now be destroyed due to center torso destruction
        unit.Events.Count.ShouldBe(initialEventCount + 2); // Should have added critical hit and destruction events
        unit.Events[^1].Type.ShouldBe(UiEventType.UnitDestroyed);
        unit.Events[^1].Parameters[0].ShouldBe(unit.Name);
    }

    [Fact]
    public void ApplyCriticalHits_WithNonDestructiveCriticalHits_ShouldNotDestroyUnit()
    {
        // Arrange - This verifies lines 491-494 are not executed when the unit is not destroyed
        var unit = CreateTestUnit();
        var targetPart = unit.Parts[PartLocation.LeftArm];
        var component = new TestComponent("Test Component", 3);
        targetPart.TryAddComponent(component);

        var initialDestroyedStatus = unit.IsDestroyed;
        var initialEventCount = unit.Events.Count;

        var hitLocations = new List<LocationCriticalHitsData>
        {
            new(PartLocation.LeftArm, [4, 4], 1,
                [
                    new ComponentHitData
                    {
                        Slot = component.MountedAtFirstLocationSlots[0],
                        Type = MakaMekComponent.MachineGun
                    }
                ],
                false)
        };

        // Act
        unit.ApplyCriticalHits(hitLocations);

        // Assert
        initialDestroyedStatus.ShouldBeFalse(); // Unit was not destroyed initially
        unit.IsDestroyed.ShouldBeFalse(); // Unit should still not be destroyed
        unit.Events.Count.ShouldBeGreaterThan(initialEventCount); // Critical hits should generate events
        unit.Events.Where(e => e.Type == UiEventType.UnitDestroyed).ShouldBeEmpty(); // But no destruction event
    }
    
    [Fact]
    public void ApplyDamage_WithUnitDestruction_ShouldAddUnitDestroyedEvent()
    {
        // Arrange
        var unit = CreateTestUnit();
        
        // Create hit locations that will destroy the unit
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(
                PartLocation.CenterTorso, // Location
                100, // Damage enough to destroy the center torso completely
                [], // No aimed shot
                [] // No location roll
            )
        };
        
        // Act
        unit.ApplyDamage(hitLocations, HitDirection.Front);
        
        // Assert
        unit.Status.ShouldBe(UnitStatus.Destroyed);
        
        // Dequeue all events and check for unit destroyed event
        var foundUnitDestroyedEvent = false;
        while (unit.DequeueNotification() is { } uiEvent)
        {
            if (uiEvent.Type == UiEventType.UnitDestroyed && uiEvent.Parameters[0]?.ToString() == unit.Name)
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
            ExternalHeatSources = [],
            DissipationData = default
        });

        // Assert
        // Verify no heat penalty for base unit
        sut.CurrentHeat.ShouldBe(15);
        sut.MovementHeatPenalty.ShouldBeNull();
        sut.AttackHeatPenalty.ShouldBeNull();
    }

    [Fact]
    public void EngineHeatSinks_ShouldBeZero_ByDefault()
    {
        var sut = CreateTestUnit();
        
        sut.EngineHeatSinks.ShouldBe(0);
    }
    
    [Fact]
    public void EngineHeatPenalty_ReturnsNull_ForBaseUnit()
    {
        // Arrange
        var sut = CreateTestUnit();
        
        // Act
        var engineHeatPenalty = sut.EngineHeatPenalty;
        
        // Assert
        engineHeatPenalty.ShouldBeNull();
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
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(PartLocation.CenterTorso, 5, [], []), // No aimed shot, no location roll
            CreateHitDataForLocation(PartLocation.LeftArm, 3, [], []) // No aimed shot, no location roll
        };

        // Act
        sut.ApplyDamage(hitLocations, HitDirection.Front);

        // Assert
        sut.TotalPhaseDamage.ShouldBe(8); // 5 + 3
    }

    [Fact]
    public void ApplyDamage_WithMultipleCalls_ShouldAccumulateTotalPhaseDamage()
    {
        // Arrange
        var sut = CreateTestUnit();
        var firstHitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(PartLocation.CenterTorso, 4, [], []) // No aimed shot, no location roll
        };
        var secondHitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(PartLocation.LeftArm, 6, [], []) // No aimed shot, no location roll
        };

        // Act
        sut.ApplyDamage(firstHitLocations, HitDirection.Front);
        sut.ApplyDamage(secondHitLocations, HitDirection.Front);

        // Assert
        sut.TotalPhaseDamage.ShouldBe(10); // 4 + 6
    }

    [Fact]
    public void ApplyCriticalHits_WithExplosionDamage_ShouldIncludeItInTotalPhaseDamage()
    {
        // Arrange
        var sut = CreateTestUnit();
        var pilot = new MechWarrior("John", "Doe");
        sut.AssignPilot(pilot);
        var targetPart = sut.Parts[PartLocation.LeftArm];
        
        // Create an explodable component
        var explodableComponent = new TestExplodableComponent("Explodable Component", 3);
        targetPart.TryAddComponent(explodableComponent, [1]);
        
        var hitLocations = new List<LocationCriticalHitsData>
        {
            new(PartLocation.LeftArm, [4, 4], 1,
                [
                    new ComponentHitData
                    {
                        Slot = explodableComponent.MountedAtFirstLocationSlots[0],
                        Type = MakaMekComponent.MachineGun,
                        ExplosionDamage = 3,
                        ExplosionDamageDistribution = [
                            new LocationDamageData(PartLocation.LeftArm, 0, 3, false)
                        ]
                    }
                ],
                false) 
        };
        var initialTotalDamage = sut.TotalPhaseDamage;

        // Act
        sut.ApplyCriticalHits(hitLocations);

        // Assert
        sut.TotalPhaseDamage.ShouldBe(initialTotalDamage + 3); 
    }

    [Fact]
    public void ResetPhase_ShouldResetTotalPhaseDamageToZero()
    {
        // Arrange
        var sut = CreateTestUnit();
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(PartLocation.CenterTorso, 7, [], []), // No aimed shot, no location roll
            CreateHitDataForLocation(PartLocation.LeftArm, 4, [], []) // No aimed shot, no location roll
        };

        // Apply damage to accumulate TotalPhaseDamage
        sut.ApplyDamage(hitLocations, HitDirection.Front);
        sut.TotalPhaseDamage.ShouldBe(11); // Verify damage was accumulated

        // Act
        sut.ResetPhaseState();

        // Assert
        sut.TotalPhaseDamage.ShouldBe(0);
    }

    [Fact]
    public void IsDestroyed_ShouldReturnTrue_WhenStatusContainsDestroyedFlag()
    {
        // Arrange
        var unit = CreateTestUnit();

        // Test various combinations of status flags that include Destroyed
        var statusCombinations = new[]
        {
            UnitStatus.Destroyed,
            UnitStatus.Destroyed | UnitStatus.Active,
            UnitStatus.Destroyed | UnitStatus.Shutdown,
            UnitStatus.Destroyed | UnitStatus.Prone,
            UnitStatus.Destroyed | UnitStatus.Immobile,
            UnitStatus.Destroyed | UnitStatus.Active | UnitStatus.Prone,
            UnitStatus.Destroyed | UnitStatus.Shutdown | UnitStatus.Immobile,
            UnitStatus.Destroyed | UnitStatus.Active | UnitStatus.Shutdown | UnitStatus.Prone | UnitStatus.Immobile
        };

        foreach (var statusCombination in statusCombinations)
        {
            // Act
            unit.SetStatusForTesting(statusCombination);

            // Assert
            unit.IsDestroyed.ShouldBeTrue($"IsDestroyed should be true for status combination: {statusCombination}");
        }
    }

    [Fact]
    public void IsDestroyed_ShouldReturnFalse_WhenStatusDoesNotContainDestroyedFlag()
    {
        // Arrange
        var unit = CreateTestUnit();

        // Test various combinations of status flags that do NOT include Destroyed
        var statusCombinations = new[]
        {
            UnitStatus.None,
            UnitStatus.Active,
            UnitStatus.Shutdown,
            UnitStatus.Prone,
            UnitStatus.Immobile,
            UnitStatus.Active | UnitStatus.Prone,
            UnitStatus.Shutdown | UnitStatus.Immobile,
            UnitStatus.Active | UnitStatus.Shutdown | UnitStatus.Prone | UnitStatus.Immobile
        };

        foreach (var statusCombination in statusCombinations)
        {
            // Act
            unit.SetStatusForTesting(statusCombination);

            // Assert
            unit.IsDestroyed.ShouldBeFalse($"IsDestroyed should be false for status combination: {statusCombination}");
        }
    }
    
    [Fact]
    public void IsOutOfCommission_ShouldReturnFalse_ByDefault()
    {
        // Arrange
        var unit = CreateTestUnit();

        // Act & Assert
        unit.IsOutOfCommission.ShouldBeFalse();
    }
    
    [Fact]
    public void IsOutOfCommission_ShouldReturnTrue_WhenPilotIsDead()
    {
        // Arrange
        var unit = CreateTestUnit();
        unit.AssignPilot(new MechWarrior("John", "Doe"));
        unit.Pilot?.Kill();

        // Act & Assert
        unit.IsOutOfCommission.ShouldBeTrue();
    }
    
    [Fact]
    public void IsOutOfCommission_ShouldReturnTrue_WhenUnitIsDestroyed()
    {
        // Arrange
        var unit = CreateTestUnit();

        // Set the unit as destroyed
        unit.SetStatusForTesting(UnitStatus.Destroyed);

        // Act & Assert
        unit.IsOutOfCommission.ShouldBeTrue();
    }

    [Fact]
    public void Status_ShouldBeImmutableAfterDestruction_WhenAttemptingToChangeStatus()
    {
        // Arrange
        var unit = CreateTestUnit();

        // Set the unit as destroyed
        unit.SetStatusForTesting(UnitStatus.Destroyed);
        unit.IsDestroyed.ShouldBeTrue("Unit should be destroyed initially");

        // Test various attempts to change status after destruction
        var attemptedStatusChanges = new[]
        {
            UnitStatus.None,
            UnitStatus.Active,
            UnitStatus.Shutdown,
            UnitStatus.Prone,
            UnitStatus.Immobile,
            UnitStatus.Active | UnitStatus.Prone,
            UnitStatus.Shutdown | UnitStatus.Immobile,
            UnitStatus.Active | UnitStatus.Shutdown | UnitStatus.Prone | UnitStatus.Immobile
        };

        foreach (var attemptedStatus in attemptedStatusChanges)
        {
            // Act - Attempt to change status
            unit.SetStatusForTesting(attemptedStatus);

            // Assert - Status should remain Destroyed and not change
            unit.Status.ShouldBe(UnitStatus.Destroyed,
                $"Status should remain Destroyed after attempting to set it to: {attemptedStatus}");
            unit.IsDestroyed.ShouldBeTrue(
                $"IsDestroyed should remain true after attempting to set status to: {attemptedStatus}");
        }
    }

    [Fact]
    public void IsImmobile_ShouldReturnFalse_ForBaseUnit_RegardlessOfStatusSet()
    {
        // Arrange
        var sut = CreateTestUnit();

        // Act
        sut.SetStatusForTesting(UnitStatus.Immobile);

        // Assert
        sut.IsImmobile.ShouldBeFalse();
    }
    
    [Fact]
    public void CanFireWeapons_ShouldReturnTrue()
    {
        // Arrange
        var sut = CreateTestUnit();

        // Act
        var result = sut.CanFireWeapons;

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(MovementType.Walk)]
    [InlineData(MovementType.Run)]
    [InlineData(MovementType.Jump)]
    public void GetMovementPoints_ShouldReturnWalkingPoints(MovementType movementType)
    {
        // Arrange
        const int walkingPoints = 2;
        var sut = CreateTestUnit(walkMp: walkingPoints);
        
        // Act
        var result = sut.GetMovementPoints(movementType);
        
        // Assert
        result.ShouldBe(walkingPoints);
    }
    
    [Fact]
    public void DamageReducedMovement_ShouldReturnWalkingPoints()
    {
        // Arrange
        const int walkingPoints = 2;
        var sut = CreateTestUnit(walkMp: walkingPoints);
        
        // Act
        var result = sut.DamageReducedMovement;
        
        // Assert
        result.ShouldBe(walkingPoints);
    }
    
    [Fact]
    public void GetAttackModifiers_ShouldReturnEmptyList_ForBaseUnit()
    {
        // Arrange
        var sut = CreateTestUnit();
        
        // Act
        var result = sut.GetAttackModifiers(PartLocation.CenterTorso);
        
        // Assert
        result.ShouldBeEmpty();
    }
    
    [Fact]
    public void MovementModifiers_ShouldReturnEmptyList_ForBaseUnit()
    {
        // Arrange
        var sut = CreateTestUnit();
        
        // Act
        var result = sut.MovementModifiers;
        
        // Assert
        result.ShouldBeEmpty();
    }
    
    [Fact]
    public void IsMinimumMovement_ShouldReturnFalse_ByDefault()
    {
        // Arrange
        var sut = CreateTestUnit();
        
        // Act
        var result = sut.IsMinimumMovement;
        
        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void BaseMovement_ShouldBeZero_WhenNoEngine()
    {
        // Arrange
        var parts = new List<UnitPart>
        {
            new TestUnitPart("Head", PartLocation.Head, 9, 3, 6),
            new TestUnitPart("Center Torso", PartLocation.CenterTorso, 10, 5, 10),
            new TestUnitPart("Left Arm", PartLocation.LeftArm, 10, 5, 10),
            new TestUnitPart("Right Arm", PartLocation.RightArm, 10, 5, 10)
        };

        var sut = new TestUnit("Test", "Unit", 20, parts);
        
        // Assert
        sut.AvailableWalkingPoints.ShouldBe(0);
    }
    
    [Fact]
    public void BaseMovement_ShouldBeZero_WhenUnitHasNoMass()
    {
        // Arrange
        var sut = new TestUnit("Test", "Unit", 0, []);
        
        // Assert
        sut.AvailableWalkingPoints.ShouldBe(0);
    }
    
    [Fact]
    public void Remove_ShouldSetPositionToNull()
    {
        // Arrange
        var sut = CreateTestUnit();
        sut.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        sut.Position.ShouldNotBeNull();
        
        // Act
        sut.RemoveFromBoard();
        
        // Assert
        sut.Position.ShouldBeNull();
    }
    
    [Fact]
    public void AddExternalHeat_MultipleWeapons_AccumulatesHeat()
    {
        // Arrange
        _rulesProvider.GetExternalHeatCap().Returns(15);
        var sut = CreateTestUnit();
        
        // Act
        sut.AddExternalHeat("Flamer 1", 2);
        sut.AddExternalHeat("Flamer 2", 2);
        sut.AddExternalHeat("Flamer 3", 2);
        
        // Assert
        var heatData = sut.GetHeatData(_rulesProvider);
        heatData.ExternalHeatPoints.ShouldBe(6);
    }
    
    [Fact]
    public void GetHeatData_ExternalHeatPoints_WithMoreThan15Points_CapsAt15()
    {
        // Arrange
        _rulesProvider.GetExternalHeatCap().Returns(15);
        var unit = CreateTestUnit();
        
        // 10 Flamers × 2 heat each = 20 total
        for (var i = 0; i < 10; i++)
        {
            unit.AddExternalHeat($"Flamer {i}", 2);
        }
        
        // Act & Assert
        var heatData = unit.GetHeatData(_rulesProvider);
        heatData.ExternalHeatPoints.ShouldBe(15);
    }
    
    [Fact]
    public void ApplyHeat_ClearsExternalHeat()
    {
        // Arrange
        var sut = CreateTestUnit();
        sut.AddExternalHeat("Flamer", 2);
        var heatData =sut.GetHeatData(_rulesProvider);
        heatData.ExternalHeatSources.Sum(s => s.HeatPoints).ShouldBe(2);
        
        // Act
        sut.ApplyHeat(heatData) ;
        
        // Assert
        sut.GetHeatData(_rulesProvider).ExternalHeatSources.Sum(s => s.HeatPoints).ShouldBe(0);
    }
    
    [Fact]
    public void GetHeatData_WithExternalHeat_IncludesExternalHeatSources()
    {
        // Arrange
        var sut = CreateTestUnit();
        sut.AddExternalHeat("Flamer 1", 2);
        sut.AddExternalHeat("Flamer 2", 2);
        
        // Act
        var heatData = sut.GetHeatData(_rulesProvider);
        
        // Assert
        heatData.ExternalHeatSources.ShouldNotBeEmpty();
        heatData.ExternalHeatSources.Count.ShouldBe(2);
        heatData.ExternalHeatSources[0].WeaponName.ShouldBe("Flamer 1");
        heatData.ExternalHeatSources[0].HeatPoints.ShouldBe(2);
        heatData.ExternalHeatSources[1].WeaponName.ShouldBe("Flamer 2");
        heatData.ExternalHeatSources[1].HeatPoints.ShouldBe(2);
    }
    
    [Fact]
    public void GetHeatData_WithNoExternalHeat_ReturnsEmptyExternalHeatSources()
    {
        // Arrange
        var sut = CreateTestUnit();
        
        // Act
        var heatData = sut.GetHeatData(_rulesProvider);
        
        // Assert
        heatData.ExternalHeatSources.ShouldBeEmpty();
    }
    
    [Fact]
    public void GetHeatData_TotalHeatPoints_IncludesExternalHeatWithCap()
    {
        // Arrange
        _rulesProvider.GetExternalHeatCap().Returns(15);
        var sut = CreateTestUnit();
        
        // Add 10 Flamers × 2 heat each = 20 total, but capped at 15
        for (var i = 0; i < 10; i++)
        {
            sut.AddExternalHeat($"Flamer {i}", 2);
        }
        
        // Act
        var heatData = sut.GetHeatData(_rulesProvider);
        
        // Assert
        // External heat sources should show all 20 points
        heatData.ExternalHeatSources.Sum(s => s.HeatPoints).ShouldBe(20);
        
        // But TotalHeatPoints should cap external heat at 15
        heatData.TotalHeatPoints.ShouldBe(15, "TotalHeatPoints should cap external heat at 15");
    }
    
    [Fact]
    public void GetHeatData_TotalHeatPoints_CombinesAllHeatSourcesWithExternalCap()
    {
        // Arrange
        _rulesProvider.GetExternalHeatCap().Returns(15);
        var sut = CreateTestUnit();
        var deployPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        sut.Deploy(deployPosition);
        
        // Add movement heat
        sut.Move(new MovementPath([
            new PathSegment(deployPosition, deployPosition, 5)], MovementType.Run));
        
        // Add external heat (10 Flamers × 2 = 20, capped at 15)
        for (var i = 0; i < 10; i++)
        {
            sut.AddExternalHeat($"Flamer {i}", 2);
        }
        
        _rulesProvider.GetMovementHeatPoints(MovementType.Run, 5).Returns(2);
        
        // Act
        var heatData = sut.GetHeatData(_rulesProvider);
        
        // Assert
        // Movement: 2, External: 15 (capped from 20)
        heatData.TotalHeatPoints.ShouldBe(17);
    }
    
    [Fact]
    public void ApplyHeat_WithExternalHeat_IncreasesCurrentHeat()
    {
        // Arrange
        var sut = CreateTestUnit();
        var initialHeat = sut.CurrentHeat;
        
        var heatData = new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = [],
            ExternalHeatSources = [
                new ExternalHeatData { WeaponName = "Flamer 1", HeatPoints = 2 },
                new ExternalHeatData { WeaponName = "Flamer 2", HeatPoints = 2 }
            ],
            DissipationData = new HeatDissipationData
            {
                HeatSinks = 0,
                EngineHeatSinks = 0,
                DissipationPoints = 0
            }
        };
        
        // Act
        sut.ApplyHeat(heatData);
        
        // Assert
        sut.CurrentHeat.ShouldBe(initialHeat + 4);
    }
    
    [Fact]
    public void ApplyHeat_WithExternalHeatExceedingCap_AppliesOnlyCappedAmount()
    {
        // Arrange
        var unit = CreateTestUnit();
        var initialHeat = unit.CurrentHeat;
        
        // Create 10 external heat sources × 2 points = 20 total
        var externalHeatSources = new List<ExternalHeatData>();
        for (var i = 0; i < 10; i++)
        {
            externalHeatSources.Add(new ExternalHeatData 
            { 
                WeaponName = $"Flamer {i}", 
                HeatPoints = 2 
            });
        }
        
        var heatData = new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = [],
            ExternalHeatSources = externalHeatSources,
            DissipationData = new HeatDissipationData
            {
                HeatSinks = 0,
                EngineHeatSinks = 0,
                DissipationPoints = 0
            }
        };
        
        // Act
        unit.ApplyHeat(heatData);
        
        // Assert
        unit.CurrentHeat.ShouldBe(initialHeat + 15, "Should apply only 15 points of external heat (capped)");
    }
    
    [Fact]
    public void Facing_ShouldBeNull_WhenNotDeployed()
    {
        // Arrange
        var sut = CreateTestUnit();
        
        // Assert
        sut.Facing.ShouldBeNull();
    }
    
    [Fact]
    public void Facing_ShouldMatchPositionFacing_WhenDeployed()
    {
        // Arrange
        var sut = CreateTestUnit();
        var position = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        sut.Deploy(position);
        
        // Assert
        sut.Facing.ShouldBe(position.Facing);
    }
    
    [Fact]
    public void GetProjectedHeatValue_EqualsCurrentHeat_WhenNoWeaponsSelected()
    {
        // Arrange
        var sut = CreateTestUnit();
        sut.ApplyHeat(new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = [new WeaponHeatData { WeaponName = "Test", HeatPoints = 5 }],
            ExternalHeatSources = [],
            DissipationData = new HeatDissipationData
            {
                HeatSinks = 0,
                EngineHeatSinks = 0,
                DissipationPoints = 0
            }
        });
        
        // Act
        var projectedHeat = sut.GetProjectedHeatValue(_rulesProvider);
        
        // Assert
        projectedHeat.ShouldBe(sut.CurrentHeat);
        projectedHeat.ShouldBe(5);
    }

    [Fact]
    public void GetProjectedHeatValue_IncludesSelectedWeaponsHeat()
    {
        // Arrange
        var sut = CreateTestUnit();
        var target = CreateTestUnit();
        var weapon1 = new MediumLaser();
        var weapon2 = new MediumLaser();
        var leftArm = sut.Parts[PartLocation.LeftArm];
        var rightArm = sut.Parts[PartLocation.RightArm];
        leftArm.TryAddComponent(weapon1);
        rightArm.TryAddComponent(weapon2);
    
        sut.WeaponAttackState.SetWeaponTarget(weapon1, target, sut);
        sut.WeaponAttackState.SetWeaponTarget(weapon2, target, sut);
    
        sut.GetProjectedHeatValue(_rulesProvider).ShouldBe(6); // 3 + 3 = 6 heat
    }
    
    [Fact]
    public void GetProjectedHeat_UsesDeclaredWeaponHeat_WhenAttacksAreDeclared()
    {
        // Arrange
        var sut = CreateTestUnit();
        sut.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        var target = CreateTestUnit();
        var weapon1 = new MediumLaser();
        var weapon2 = new MediumLaser();
        var leftArm = sut.Parts[PartLocation.LeftArm];
        var rightArm = sut.Parts[PartLocation.RightArm];
        leftArm.TryAddComponent(weapon1);
        rightArm.TryAddComponent(weapon2);
    
        var weapon1Slot = weapon1.SlotAssignments[0].FirstSlot;
        var weapon2Slot = weapon2.SlotAssignments[0].FirstSlot;
    
        sut.DeclareWeaponAttack([
            new WeaponTargetData
            {
                Weapon = new ComponentData
                {
                    Name = "Medium Laser",
                    Type = MakaMekComponent.MediumLaser,
                    Assignments = [new LocationSlotAssignment(PartLocation.LeftArm, weapon1Slot, 1)]
                },
                TargetId = target.Id,
                IsPrimaryTarget = true
            },
            new WeaponTargetData
            {
                Weapon = new ComponentData
                {
                    Name = "Medium Laser",
                    Type = MakaMekComponent.MediumLaser,
                    Assignments = [new LocationSlotAssignment(PartLocation.RightArm, weapon2Slot, 1)]
                },
                TargetId = target.Id,
                IsPrimaryTarget = true
            }
        ]);
    
        sut.GetProjectedHeatValue(_rulesProvider).ShouldBe(6);
    }
    
    [Fact]
    public void IsWeaponConfigurationApplied_ShouldReturnFalse()
    {
        // Arrange
        var sut = CreateTestUnit();
        
        // Act & Assert
        sut.IsWeaponConfigurationApplied(new WeaponConfiguration
        {
            Type = WeaponConfigurationType.TorsoRotation,
            Value = (int)HexDirection.Bottom
        }).ShouldBeFalse();
    }
    
    [Fact]
    public void GetAvailableMovementTypes_ShouldReturnWalkOnly()
    {
        // Arrange
        var sut = CreateTestUnit();
        
        // Act
        var result = sut.GetAvailableMovementTypes();
        
        // Assert
        result.ShouldContain(MovementType.Walk);
        result.Count.ShouldBe(1);
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
    
    private LocationHitData CreateHitDataForLocation(PartLocation partLocation,
        int damage,
        int[]? aimedShotRoll = null,
        int[]? locationRoll = null)
    {
        return new LocationHitData(
        [
            new LocationDamageData(partLocation,
                damage-1,
                1,
                false)
        ], aimedShotRoll??[], locationRoll??[], partLocation);
    }
}
