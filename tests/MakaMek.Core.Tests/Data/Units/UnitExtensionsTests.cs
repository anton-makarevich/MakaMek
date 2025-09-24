using NSubstitute;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Utils;
using Shouldly;
using Sanet.MakaMek.Core.Tests.Utils;

namespace Sanet.MakaMek.Core.Tests.Data.Units;

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
}
