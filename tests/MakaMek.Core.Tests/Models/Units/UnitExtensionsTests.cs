using NSubstitute;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units;

public class UnitExtensionsTests
{
    private readonly MechFactory _mechFactory;
    private readonly UnitData _originalUnitData;
    private readonly IRulesProvider _rulesProvider = new ClassicBattletechRulesProvider();
    private readonly IComponentProvider _componentProvider = new ClassicBattletechComponentProvider();

    public UnitExtensionsTests()
    {
        _originalUnitData = MechFactoryTests.CreateDummyMechData();
        _originalUnitData.Id = Guid.NewGuid();
        _mechFactory = new MechFactory(_rulesProvider, _componentProvider, Substitute.For<ILocalizationService>());
    }

    [Fact]
    public void ToData_ConvertsMechToUnitData_WithSameBasicProperties()
    {
        // Arrange
        var mech = _mechFactory.Create(_originalUnitData);

        // Act
        var convertedUnitData = mech.ToData();

        // Assert
        convertedUnitData.Chassis.ShouldBe(_originalUnitData.Chassis);
        convertedUnitData.Model.ShouldBe(_originalUnitData.Model);
        convertedUnitData.Mass.ShouldBe(_originalUnitData.Mass);
        convertedUnitData.EngineRating.ShouldBe(_originalUnitData.EngineRating);
        convertedUnitData.EngineType.ShouldBe(_originalUnitData.EngineType);
        convertedUnitData.Position.ShouldBeNull();
    }

    [Fact]
    public void ToData_WithProneStatus_SerializesProneFlag()
    {
        // Arrange
        var mech = _mechFactory.Create(_originalUnitData);
        mech.SetProne();

        // Act
        var convertedUnitData = mech.ToData();

        // Assert
        convertedUnitData.StatusFlags.ShouldNotBeNull();
        convertedUnitData.StatusFlags.ShouldContain(UnitStatus.Prone);
        convertedUnitData.StatusFlags.ShouldNotContain(UnitStatus.None);
        convertedUnitData.StatusFlags.ShouldNotContain(UnitStatus.Active);
    }

    [Fact]
    public void ToData_WithImmobileStatus_SerializesImmobileFlag()
    {
        // Arrange
        var mech = _mechFactory.Create(_originalUnitData);
        var pilot = new MechWarrior("John", "Doe");
        mech.AssignPilot(pilot);
        pilot.KnockUnconscious(1);

        // Act
        var convertedUnitData = mech.ToData();

        // Assert
        convertedUnitData.StatusFlags.ShouldNotBeNull();
        convertedUnitData.StatusFlags.ShouldContain(UnitStatus.Immobile);
        convertedUnitData.StatusFlags.ShouldNotContain(UnitStatus.None);
        convertedUnitData.StatusFlags.ShouldNotContain(UnitStatus.Active);
    }

    [Fact]
    public void ToData_WithMultipleStatusFlags_SerializesAllFlagsSeparately()
    {
        // Arrange
        var mech = _mechFactory.Create(_originalUnitData);
        mech.SetProne();
        var pilot = new MechWarrior("John", "Doe");
        mech.AssignPilot(pilot);
        pilot.KnockUnconscious(1);

        // Act
        var convertedUnitData = mech.ToData();

        // Assert
        convertedUnitData.StatusFlags.ShouldNotBeNull();
        convertedUnitData.StatusFlags.ShouldContain(UnitStatus.Prone);
        convertedUnitData.StatusFlags.ShouldContain(UnitStatus.Immobile);
        convertedUnitData.StatusFlags.ShouldNotContain(UnitStatus.None);
        convertedUnitData.StatusFlags.ShouldNotContain(UnitStatus.Active);
    }

    [Fact]
    public void ToData_WithNoSpecialStatusFlags_SetsStatusFlagsToNull()
    {
        // Arrange
        var mech = _mechFactory.Create(_originalUnitData);
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        mech.AssignPilot(pilot);

        // Act
        var convertedUnitData = mech.ToData();

        // Assert
        convertedUnitData.StatusFlags.ShouldBeNull();
    }
    
    [Fact]
    public void ToData_ConvertsPosition_WhenDeployed()
    {
        // Arrange
        var mech = _mechFactory.Create(_originalUnitData);
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        mech.Deploy(position);

        // Act
        var convertedUnitData = mech.ToData();

        // Assert
        convertedUnitData.Position.ShouldNotBeNull();
        convertedUnitData.Position.Coordinates.Q.ShouldBe(1);
        convertedUnitData.Position.Coordinates.R.ShouldBe(1);
        convertedUnitData.Position.Facing.ShouldBe(3);
    }

    [Fact]
    public void ToData_ConvertsMechToUnitData_WithSameArmorValues()
    {
        // Arrange
        var mech = _mechFactory.Create(_originalUnitData);

        // Act
        var convertedUnitData = mech.ToData();

        // Assert
        // Verify that all armor locations match
        foreach (var location in _originalUnitData.ArmorValues.Keys)
        {
            convertedUnitData.ArmorValues.ContainsKey(location).ShouldBeTrue();
            
            var originalArmor = _originalUnitData.ArmorValues[location];
            var convertedArmor = convertedUnitData.ArmorValues[location];
            
            convertedArmor.FrontArmor.ShouldBe(originalArmor.FrontArmor);
            convertedArmor.RearArmor.ShouldBe(originalArmor.RearArmor);
        }
    }

    [Fact]
    public void ToData_ConvertsMechToUnitData_WithSameEquipment()
    {
        // Arrange
        var mech = _mechFactory.Create(_originalUnitData);

        // Act
        var convertedUnitData = mech.ToData();

        // Assert
        // Verify that all locations have the same equipment
        foreach (var originalComponent in _originalUnitData.Equipment)
        {
            var convertedComponent = convertedUnitData.Equipment.FirstOrDefault(cd =>
                cd.Type == originalComponent.Type
                && cd.Assignments.SequenceEqual(originalComponent.Assignments));
            convertedComponent.ShouldNotBeNull();
        }
    }
    
    [Fact]
    public void ToData_ConvertsMechToUnitData_WithEngine()
    {
        // Arrange
        var mech = _mechFactory.Create(_originalUnitData);

        // Act
        var convertedUnitData = mech.ToData();

        // Assert
        convertedUnitData.EngineRating.ShouldBe(_originalUnitData.EngineRating);
        convertedUnitData.EngineType.ShouldBe(_originalUnitData.EngineType);
        
        // Verify that the engine is in the center torso
        var engineComponent = convertedUnitData.Equipment.FirstOrDefault(cd => cd.Type == MakaMekComponent.Engine);
        engineComponent.ShouldNotBeNull();
        engineComponent.Assignments.Count.ShouldBe(2);
        engineComponent.Assignments[0].Location.ShouldBe(PartLocation.CenterTorso);
        engineComponent.Assignments[0].FirstSlot.ShouldBe(0);
        engineComponent.Assignments[0].Length.ShouldBe(3);
        engineComponent.Assignments[1].Location.ShouldBe(PartLocation.CenterTorso);
        engineComponent.Assignments[1].FirstSlot.ShouldBe(7);
        engineComponent.Assignments[1].Length.ShouldBe(3);
    }
    
    [Fact]
    public void ToData_ConvertsMechToOriginalUnitData()
    {
        // Arrange
        var mech = _mechFactory.Create(_originalUnitData);

        // Act
        var convertedUnitData = mech.ToData();

        // Assert - Compare properties directly instead of comparing JSON strings
        // This ignores the order of dictionary keys
        
        // Compare basic properties
        convertedUnitData.Id.ShouldBe(_originalUnitData.Id);
        convertedUnitData.Chassis.ShouldBe(_originalUnitData.Chassis);
        convertedUnitData.Model.ShouldBe(_originalUnitData.Model);
        convertedUnitData.Mass.ShouldBe(_originalUnitData.Mass);
        convertedUnitData.EngineRating.ShouldBe(_originalUnitData.EngineRating);
        convertedUnitData.EngineType.ShouldBe(_originalUnitData.EngineType);
        
        // Compare ArmorValues
        foreach (var location in _originalUnitData.ArmorValues.Keys)
        {
            convertedUnitData.ArmorValues.ContainsKey(location).ShouldBeTrue();
            
            var originalArmor = _originalUnitData.ArmorValues[location];
            var convertedArmor = convertedUnitData.ArmorValues[location];
            
            convertedArmor.FrontArmor.ShouldBe(originalArmor.FrontArmor);
            convertedArmor.RearArmor.ShouldBe(originalArmor.RearArmor);
        }
        
        // Compare LocationEquipment - check that both dictionaries have the same keys
        _originalUnitData.Equipment.Count.ShouldBe(convertedUnitData.Equipment.Count);
        foreach (var originalComponent in _originalUnitData.Equipment)
        {
            var convertedComponent = convertedUnitData.Equipment.FirstOrDefault(cd =>
                cd.Type == originalComponent.Type
                && cd.Assignments.SequenceEqual(originalComponent.Assignments));
            convertedComponent.ShouldNotBeNull();
        }
        
        // Check AdditionalAttributes and Quirks
        convertedUnitData.AdditionalAttributes.Count.ShouldBe(_originalUnitData.AdditionalAttributes.Count);
        convertedUnitData.Quirks.Count.ShouldBe(_originalUnitData.Quirks.Count);
    }
    
    [Fact]
    public void ToData_WithNoDamage_ShouldNotIncludePartStates()
    {
        // Arrange - Create a pristine mech
        var mech = _mechFactory.Create(_originalUnitData);

        // Act
        var serializedData = mech.ToData();

        // Assert
        serializedData.UnitPartStates.ShouldBeNull();
    }

    [Fact]
    public void ToData_WithBlownOffArm_ShouldSerializePartState()
    {
        // Arrange
        var mech = _mechFactory.Create(_originalUnitData);
        
        // Blow off the right arm
        var rightArm = mech.Parts[PartLocation.RightArm];
        rightArm.BlowOff();

        // Act
        var serializedData = mech.ToData();

        // Assert
        serializedData.UnitPartStates.ShouldNotBeNull();
        serializedData.UnitPartStates.Count.ShouldBe(1);
        
        var armState = serializedData.UnitPartStates.First(s => s.Location == PartLocation.RightArm);
        armState.IsBlownOff.ShouldBeTrue();
    }

    [Fact]
    public void ToData_WithDestroyedArmNoStructure_ShouldSerializePartState()
    {
        // Arrange
        var mech = _mechFactory.Create(_originalUnitData);
        
        // Destroy the left arm completely (deplete all armor and structure)
        var leftArm = mech.Parts[PartLocation.LeftArm];
        var totalDamage = leftArm.MaxArmor + leftArm.MaxStructure;
        leftArm.ApplyDamage(totalDamage, HitDirection.Front);

        // Act
        var serializedData = mech.ToData();

        // Assert
        serializedData.UnitPartStates.ShouldNotBeNull();
        serializedData.UnitPartStates.Count.ShouldBe(1);
        
        var armState = serializedData.UnitPartStates.First(s => s.Location == PartLocation.LeftArm);
        armState.CurrentFrontArmor.ShouldBe(0);
        armState.CurrentStructure.ShouldBe(0);
        armState.IsBlownOff.ShouldBeFalse();
    }

    [Fact]
    public void ToData_WithRearArmorDamageOnSideTorso_ShouldSerializePartState()
    {
        // Arrange
        var mech = _mechFactory.Create(_originalUnitData);
        
        // Damage rear armor on left torso
        var leftTorso = (SideTorso)mech.Parts[PartLocation.LeftTorso];
        var rearArmorDamage = 5;
        leftTorso.ApplyDamage(rearArmorDamage, HitDirection.Rear);

        // Act
        var serializedData = mech.ToData();

        // Assert
        serializedData.UnitPartStates.ShouldNotBeNull();
        serializedData.UnitPartStates.Count.ShouldBe(1);
        
        var torsoState = serializedData.UnitPartStates.First(s => s.Location == PartLocation.LeftTorso);
        torsoState.CurrentRearArmor.ShouldBe(leftTorso.CurrentRearArmor);
        torsoState.CurrentFrontArmor.ShouldBe(leftTorso.MaxArmor); // Front armor untouched
    }

    [Fact]
    public void ToData_WithFrontArmorDamageOnSideTorso_ShouldSerializePartState()
    {
        // Arrange
        var mech = _mechFactory.Create(_originalUnitData);
        
        // Damage front armor on right torso
        var rightTorso = (SideTorso)mech.Parts[PartLocation.RightTorso];
        var frontArmorDamage = 8;
        rightTorso.ApplyDamage(frontArmorDamage, HitDirection.Front);

        // Act
        var serializedData = mech.ToData();

        // Assert
        serializedData.UnitPartStates.ShouldNotBeNull();
        serializedData.UnitPartStates.Count.ShouldBe(1);
        
        var torsoState = serializedData.UnitPartStates.First(s => s.Location == PartLocation.RightTorso);
        torsoState.CurrentFrontArmor.ShouldBe(rightTorso.CurrentArmor);
        torsoState.CurrentRearArmor.ShouldBe(rightTorso.MaxRearArmor); // Rear armor untouched
    }

    [Fact]
    public void ToData_WithArmorDepletedAndStructureDamage_ShouldSerializePartState()
    {
        // Arrange
        var mech = _mechFactory.Create(_originalUnitData);
        
        // Deplete armor and damage structure on the center torso
        var centerTorso = (CenterTorso)mech.Parts[PartLocation.CenterTorso];
        var damage = centerTorso.MaxArmor + 5; // Deplete armor and damage 5 structure
        centerTorso.ApplyDamage(damage, HitDirection.Front);

        // Act
        var serializedData = mech.ToData();

        // Assert
        serializedData.UnitPartStates.ShouldNotBeNull();
        serializedData.UnitPartStates.Count.ShouldBe(1);
        
        var torsoState = serializedData.UnitPartStates.First(s => s.Location == PartLocation.CenterTorso);
        torsoState.CurrentFrontArmor.ShouldBe(0);
        torsoState.CurrentStructure.ShouldBe(centerTorso.CurrentStructure);
    }

    [Fact]
    public void ToData_WithMultipleDamagedParts_ShouldSerializeAllPartStates()
    {
        // Arrange
        var mech = _mechFactory.Create(_originalUnitData);
        
        // Damage multiple parts in different ways
        // 1. Blow off right arm
        mech.Parts[PartLocation.RightArm].BlowOff();
        
        // 2. Destroy the left arm (no structure left)
        var leftArm = mech.Parts[PartLocation.LeftArm];
        leftArm.ApplyDamage(leftArm.MaxArmor + leftArm.MaxStructure, HitDirection.Front);
        
        // 3. Damage rear armor on left torso
        var leftTorso = (SideTorso)mech.Parts[PartLocation.LeftTorso];
        leftTorso.ApplyDamage(5, HitDirection.Rear);
        
        // 4. Damage front armor on right torso
        var rightTorso = (SideTorso)mech.Parts[PartLocation.RightTorso];
        rightTorso.ApplyDamage(8, HitDirection.Front);
        
        // 5. Deplete armor and damage structure on the center torso
        var centerTorso = (CenterTorso)mech.Parts[PartLocation.CenterTorso];
        centerTorso.ApplyDamage(centerTorso.MaxArmor + 5, HitDirection.Front);

        // Act
        var serializedData = mech.ToData();

        // Assert
        serializedData.UnitPartStates.ShouldNotBeNull();
        serializedData.UnitPartStates.Count.ShouldBe(5);
        
        // Verify each part state
        var rightArmState = serializedData.UnitPartStates.First(s => s.Location == PartLocation.RightArm);
        rightArmState.IsBlownOff.ShouldBeTrue();
        
        var leftArmState = serializedData.UnitPartStates.First(s => s.Location == PartLocation.LeftArm);
        leftArmState.CurrentFrontArmor.ShouldBe(0);
        leftArmState.CurrentStructure.ShouldBe(0);
        
        var leftTorsoState = serializedData.UnitPartStates.First(s => s.Location == PartLocation.LeftTorso);
        leftTorsoState.CurrentRearArmor.ShouldBe(leftTorso.CurrentRearArmor);
        
        var rightTorsoState = serializedData.UnitPartStates.First(s => s.Location == PartLocation.RightTorso);
        rightTorsoState.CurrentFrontArmor.ShouldBe(rightTorso.CurrentArmor);
        
        var centerTorsoState = serializedData.UnitPartStates.First(s => s.Location == PartLocation.CenterTorso);
        centerTorsoState.CurrentFrontArmor.ShouldBe(0);
        centerTorsoState.CurrentStructure.ShouldBe(centerTorso.CurrentStructure);
    }
    
    [Fact]
    public void ToData_WithoutMovementPath_ShouldNotSerializeMovementPath()
    {
        // Arrange
        var mech = _mechFactory.Create(_originalUnitData);
        
        // Act
        var serializedData = mech.ToData();
        
        // Assert
        serializedData.MovementPathSegments.ShouldBeNull();
    }
    
    [Fact]
    public void ToData_WithMovementPath_ShouldSerializeMovementPath()
    {
        // Arrange
        var mech = _mechFactory.Create(_originalUnitData);
        
        // Move the mech
        var startPos = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var endPos = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        var path = new MovementPath([new PathSegment(startPos, endPos, 1)], MovementType.Walk);
        mech.Deploy(startPos);
        mech.Move(path);

        // Act
        var serializedData = mech.ToData();

        // Assert
        serializedData.MovementPathSegments.ShouldNotBeNull();
        serializedData.MovementPathSegments.Count.ShouldBe(1);
    }

    [Fact]
    public void CloneUnit_CreatesCopyWithSameBasicProperties()
    {
        // Arrange
        var originalMech = _mechFactory.Create(_originalUnitData);

        // Act
        var clonedMech = originalMech.CloneUnit(_mechFactory);

        // Assert
        clonedMech.ShouldNotBeNull();
        clonedMech.ShouldNotBeSameAs(originalMech); // Should be a different instance
        clonedMech.Id.ShouldBe(originalMech.Id);
        clonedMech.Chassis.ShouldBe(originalMech.Chassis);
        clonedMech.Model.ShouldBe(originalMech.Model);
        clonedMech.Tonnage.ShouldBe(originalMech.Tonnage);
    }

    [Fact]
    public void CloneUnit_CreatesCopyWithSameArmorValues()
    {
        // Arrange
        var originalMech = _mechFactory.Create(_originalUnitData);

        // Act
        var clonedMech = originalMech.CloneUnit(_mechFactory);

        // Assert
        foreach (var location in originalMech.Parts.Keys)
        {
            var originalPart = originalMech.Parts[location];
            var clonedPart = clonedMech.Parts[location];

            clonedPart.CurrentArmor.ShouldBe(originalPart.CurrentArmor);
            clonedPart.CurrentStructure.ShouldBe(originalPart.CurrentStructure);

            if (originalPart is Torso originalTorso && clonedPart is Torso clonedTorso)
            {
                clonedTorso.CurrentRearArmor.ShouldBe(originalTorso.CurrentRearArmor);
            }
        }
    }

    [Fact]
    public void CloneUnit_CreatesCopyWithDamagedState()
    {
        // Arrange
        var originalMech = _mechFactory.Create(_originalUnitData);
        
        // Damage the original mech
        var centerTorso = originalMech.Parts[PartLocation.CenterTorso];
        centerTorso.ApplyDamage(10, HitDirection.Front);

        // Act
        var clonedMech = originalMech.CloneUnit(_mechFactory);

        // Assert
        var clonedCenterTorso = clonedMech.Parts[PartLocation.CenterTorso];
        clonedCenterTorso.CurrentArmor.ShouldBe(centerTorso.CurrentArmor);
        clonedCenterTorso.CurrentStructure.ShouldBe(centerTorso.CurrentStructure);
    }

    [Fact]
    public void CloneUnit_CreatesIndependentCopy()
    {
        // Arrange
        var originalMech = _mechFactory.Create(_originalUnitData);
        var clonedMech = originalMech.CloneUnit(_mechFactory);

        // Get original armor values
        var originalCenterTorso = originalMech.Parts[PartLocation.CenterTorso];
        var originalArmor = originalCenterTorso.CurrentArmor;

        // Act - Damage the cloned mech
        var clonedCenterTorso = clonedMech.Parts[PartLocation.CenterTorso];
        clonedCenterTorso.ApplyDamage(5, HitDirection.Front);

        // Assert - Original should be unchanged
        originalCenterTorso.CurrentArmor.ShouldBe(originalArmor);
        clonedCenterTorso.CurrentArmor.ShouldBe(originalArmor - 5);
    }

    [Fact]
    public void CloneUnit_PreservesBlownOffState()
    {
        // Arrange
        var originalMech = _mechFactory.Create(_originalUnitData);
        
        // Blow off the right arm
        originalMech.Parts[PartLocation.RightArm].BlowOff();

        // Act
        var clonedMech = originalMech.CloneUnit(_mechFactory);

        // Assert
        var clonedRightArm = clonedMech.Parts[PartLocation.RightArm];
        clonedRightArm.IsBlownOff.ShouldBeTrue();
    }

    [Fact]
    public void CloneUnit_PreservesDestroyedState()
    {
        // Arrange
        var originalMech = _mechFactory.Create(_originalUnitData);
        
        // Destroy the left arm
        var leftArm = originalMech.Parts[PartLocation.LeftArm];
        leftArm.ApplyDamage(leftArm.MaxArmor + leftArm.MaxStructure, HitDirection.Front);

        // Act
        var clonedMech = originalMech.CloneUnit(_mechFactory);

        // Assert
        var clonedLeftArm = clonedMech.Parts[PartLocation.LeftArm];
        clonedLeftArm.IsDestroyed.ShouldBeTrue();
        clonedLeftArm.CurrentArmor.ShouldBe(0);
        clonedLeftArm.CurrentStructure.ShouldBe(0);
    }
    
    [Fact]
    public void GetTacticalRole_WhenUnitHas20PlusLRMTubes_ReturnsLrmBoat()
    {
        // Arrange
        var unit = Substitute.For<IUnit>();
        
        // Let's rely on creating real objects since they are data classes mostly.
        var lrm20 = new Lrm20();
        unit.GetAvailableComponents<Weapon>().Returns([lrm20]);
        
        // Act
        var result = unit.GetTacticalRole();

        // Assert
        result.ShouldBe(UnitTacticalRole.LrmBoat);
    }

    [Fact]
    public void GetTacticalRole_WhenUnitHasHighWalkMP_ReturnsScout()
    {
        // Arrange
        var unit = Substitute.For<IUnit>();
        unit.GetAvailableComponents<Weapon>().Returns([]);
        unit.GetMovementPoints(MovementType.Walk).Returns(6);

        // Act
        var result = unit.GetTacticalRole();

        // Assert
        result.ShouldBe(UnitTacticalRole.Scout);
    }
    
    [Fact]
    public void GetTacticalRole_WhenUnitHasTwoLrm10_ReturnsLrmBoat()
    {
        // Arrange
        var unit = Substitute.For<IUnit>();
        var lrm101 = new Lrm10();
        var lrm102 = new Lrm10();
        unit.GetAvailableComponents<Weapon>().Returns([lrm101, lrm102]);

        // Act
        var result = unit.GetTacticalRole();

        // Assert
        result.ShouldBe(UnitTacticalRole.LrmBoat);
    }

    [Fact]
    public void GetTacticalRole_WhenUnitHasLowWalkMP_ReturnsBrawler()
    {
        // Arrange
        var unit = Substitute.For<IUnit>();
        unit.GetAvailableComponents<Weapon>().Returns([]);
        unit.GetMovementPoints(MovementType.Walk).Returns(3);

        // Act
        var result = unit.GetTacticalRole();

        // Assert
        result.ShouldBe(UnitTacticalRole.Brawler);
    }
    
    [Fact]
    public void GetTacticalRole_WhenUnitHasJumpJets_ReturnsJumper()
    {
        // Arrange
        var unit = Substitute.For<IUnit>();
        unit.GetAvailableComponents<Weapon>().Returns([]);
        unit.GetMovementPoints(MovementType.Walk).Returns(4); // Not Scout (>6), Not Brawler (<4)
        unit.GetMovementPoints(MovementType.Jump).Returns(4); // Has Jump MP

        // Act
        var result = unit.GetTacticalRole();

        // Assert
        result.ShouldBe(UnitTacticalRole.Jumper);
    }

    [Fact]
    public void GetTacticalRole_WhenUnitHasAverageWalkMP_ReturnsTrooper()
    {
        // Arrange
        var unit = Substitute.For<IUnit>();
        unit.GetAvailableComponents<Weapon>().Returns([]);
        unit.GetMovementPoints(MovementType.Walk).Returns(4); // Not Scout (>6), Not Brawler (<4)
        unit.GetMovementPoints(MovementType.Jump).Returns(0); // No Jump

        // Act
        var result = unit.GetTacticalRole();

        // Assert
        result.ShouldBe(UnitTacticalRole.Trooper);
    }
}

