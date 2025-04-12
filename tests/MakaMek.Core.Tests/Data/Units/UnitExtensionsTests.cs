using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;
using Sanet.MakaMek.Core.Tests.Data.Community;

namespace Sanet.MakaMek.Core.Tests.Data.Units;

public class UnitExtensionsTests
{
    private readonly MechFactory _mechFactory;
    private readonly UnitData _originalUnitData;
    private readonly IRulesProvider _rulesProvider = new ClassicBattletechRulesProvider();

    public UnitExtensionsTests()
    {
        _originalUnitData = MechFactoryTests.CreateDummyMechData();
        _originalUnitData.Id = Guid.NewGuid();
        _mechFactory = new MechFactory(_rulesProvider);
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
        convertedUnitData.WalkMp.ShouldBe(_originalUnitData.WalkMp);
        convertedUnitData.EngineRating.ShouldBe(_originalUnitData.EngineRating);
        convertedUnitData.EngineType.ShouldBe(_originalUnitData.EngineType);
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
        foreach (var location in _originalUnitData.LocationEquipment.Keys)
        {
            convertedUnitData.LocationEquipment.ContainsKey(location).ShouldBeTrue();
            
            var originalEquipment = _originalUnitData.LocationEquipment[location];
            var convertedEquipment = convertedUnitData.LocationEquipment[location];
            
            // Check that the equipment lists contain the same items (ignoring order)
            convertedEquipment.Count.ShouldBe(originalEquipment.Count);
            
            foreach (var item in originalEquipment)
            {
                convertedEquipment.ShouldContain(item);
                convertedEquipment.Count(e => e == item).ShouldBe(originalEquipment.Count(e => e == item));
            }
        }
    }

    [Fact]
    public void ToData_ConvertsMechToUnitData_WithMultiSlotComponents()
    {
        // Arrange
        var locationEquipment = Tuple.Create(PartLocation.LeftTorso, new List<MakaMekComponent> 
        { 
            MakaMekComponent.AC5,
            MakaMekComponent.AC5,
            MakaMekComponent.AC5,
            MakaMekComponent.AC5 
        });
        var originalData = MechFactoryTests.CreateDummyMechData(locationEquipment);
        var mech = _mechFactory.Create(originalData);

        // Act
        var convertedUnitData = mech.ToData();

        // Assert
        // Verify that the multi-slot component is properly represented
        convertedUnitData.LocationEquipment.ContainsKey(PartLocation.LeftTorso).ShouldBeTrue();
        var convertedEquipment = convertedUnitData.LocationEquipment[PartLocation.LeftTorso];
        
        // Should have 1 AC5 entry for the single AC5 component (which takes 4 slots)
        convertedEquipment.Count(e => e == MakaMekComponent.AC5).ShouldBe(1);
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
        convertedUnitData.LocationEquipment.ContainsKey(PartLocation.CenterTorso).ShouldBeTrue();
        convertedUnitData.LocationEquipment[PartLocation.CenterTorso].ShouldContain(MakaMekComponent.Engine);
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
        convertedUnitData.WalkMp.ShouldBe(_originalUnitData.WalkMp);
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
        _originalUnitData.LocationEquipment.Keys.Count.ShouldBe(convertedUnitData.LocationEquipment.Keys.Count);
        foreach (var location in _originalUnitData.LocationEquipment.Keys)
        {
            convertedUnitData.LocationEquipment.ContainsKey(location).ShouldBeTrue();
            
            var originalEquipment = _originalUnitData.LocationEquipment[location];
            var convertedEquipment = convertedUnitData.LocationEquipment[location];
            
            // Check that the equipment lists contain the same items (ignoring order)
            convertedEquipment.Count.ShouldBe(originalEquipment.Count);
            
            foreach (var item in originalEquipment)
            {
                convertedEquipment.ShouldContain(item);
                convertedEquipment.Count(e => e == item).ShouldBe(originalEquipment.Count(e => e == item));
            }
        }
        
        // Check AdditionalAttributes and Quirks
        convertedUnitData.AdditionalAttributes.Count.ShouldBe(_originalUnitData.AdditionalAttributes.Count);
        convertedUnitData.Quirks.Count.ShouldBe(_originalUnitData.Quirks.Count);
    }
}
