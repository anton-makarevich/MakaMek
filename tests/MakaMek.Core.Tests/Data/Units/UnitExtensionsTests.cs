using NSubstitute;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Utils;
using Shouldly;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Utils;

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
        _mechFactory = new MechFactory(_rulesProvider, Substitute.For<ILocalizationService>());
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

            // Check that the equipment slot layouts contain the same components
            var originalComponents = originalEquipment.ComponentAssignments.SelectMany(ca => ca.Slots.Select(s => ca.Component)).ToList();
            var convertedComponents = convertedEquipment.ComponentAssignments.SelectMany(ca => ca.Slots.Select(s => ca.Component)).ToList();

            convertedComponents.Count.ShouldBe(originalComponents.Count);

            foreach (var item in originalComponents)
            {
                convertedComponents.ShouldContain(item);
                convertedComponents.Count(e => e == item).ShouldBe(originalComponents.Count(e => e == item));
            }
        }
    }

    // TODO: Add test for ToData() method with new Equipment model when implemented

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
        var centerTorsoLayout = convertedUnitData.LocationEquipment[PartLocation.CenterTorso];
        var engineAssignments = centerTorsoLayout.ComponentAssignments.Where(ca => ca.Component == MakaMekComponent.Engine).ToList();
        engineAssignments.Count.ShouldBe(1);
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

            // Check that the equipment slot layouts contain the same components
            var originalComponents = originalEquipment.ComponentAssignments.SelectMany(ca => ca.Slots.Select(s => ca.Component)).ToList();
            var convertedComponents = convertedEquipment.ComponentAssignments.SelectMany(ca => ca.Slots.Select(s => ca.Component)).ToList();

            convertedComponents.Count.ShouldBe(originalComponents.Count);

            foreach (var item in originalComponents)
            {
                convertedComponents.ShouldContain(item);
                convertedComponents.Count(e => e == item).ShouldBe(originalComponents.Count(e => e == item));
            }
        }
        
        // Check AdditionalAttributes and Quirks
        convertedUnitData.AdditionalAttributes.Count.ShouldBe(_originalUnitData.AdditionalAttributes.Count);
        convertedUnitData.Quirks.Count.ShouldBe(_originalUnitData.Quirks.Count);
    }
}
