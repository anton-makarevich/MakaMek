using System.Reflection;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Events;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;

namespace Sanet.MakaMek.Core.Tests.Models.Units;

public class UnitPartTests
{
    private class TestUnitPart(PartLocation location, int maxArmor, int maxStructure, int slots =12)
        : UnitPart("Test", location, maxArmor, maxStructure, slots)
    {
        internal override bool CanBeBlownOff =>true;

        public override IReadOnlyList<WeaponConfigurationOptions> GetWeaponsConfigurationOptions()
        {
            return [];
        }
    }

    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5);

        // Assert
        sut.Location.ShouldBe(PartLocation.LeftArm);
        sut.MaxArmor.ShouldBe(10);
        sut.CurrentArmor.ShouldBe(10);
        sut.MaxStructure.ShouldBe(5);
        sut.CurrentStructure.ShouldBe(5);
        sut.TotalSlots.ShouldBe(12);
        sut.UsedSlots.ShouldBe(0);
        sut.AvailableSlots.ShouldBe(12);
        sut.Components.ShouldBeEmpty();
        sut.IsDestroyed.ShouldBeFalse();
        sut.GetNextTransferLocation().ShouldBeNull();
        sut.HitSlots.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(5, 10, 5, 0)] // Damage does not exceed armor
    [InlineData(10, 10, 5, 0)] // Damage does exceed armor but structure remains
    [InlineData(20, 10, 5, 5)] // Damage exceeds armor and structure
    public void ApplyDamage_HandlesArmor(int damage, int maxArmor, int maxStructure, int expectedExcess)
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, maxArmor, maxStructure);

        // Act
        var excessDamage = sut.ApplyDamage(damage, HitDirection.Front);

        // Assert
        excessDamage.ShouldBe(expectedExcess);
        
        if (damage <= maxArmor)
        {
            sut.CurrentArmor.ShouldBe(maxArmor - damage);
            sut.CurrentStructure.ShouldBe(maxStructure);
            sut.IsDestroyed.ShouldBeFalse();
        }
        else if (damage < maxArmor + maxStructure)
        {
            sut.CurrentArmor.ShouldBe(0);
            sut.CurrentStructure.ShouldBe(maxStructure - (damage - maxArmor));
            sut.IsDestroyed.ShouldBeFalse();
        }
    }
    

    [Fact]
    public void ApplyDamage_DoesNotDestroyComponentsWhenStructureIsDestroyed()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 0, 5);
        var masc = new TestComponent("Test MASC");
        sut.TryAddComponent(masc);

        // Act
        sut.ApplyDamage(10, HitDirection.Front); // Ensure the structure is destroyed

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
        masc.IsDestroyed.ShouldBeFalse(); // Component should not be automatically destroyed
        masc.IsAvailable.ShouldBeFalse(); // not destroyed but not available to be used
        masc.IsActive.ShouldBeTrue(); // Components start active by default
    }

    [Fact]
    public void GetComponents_ReturnsCorrectComponentTypes()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5);
        var testComponent = new TestComponent("Test Component");
        sut.TryAddComponent(testComponent);

        // Act
        var testComponents = sut.GetComponents<TestComponent>().ToList();
        var jumpJetComponents = sut.GetComponents<JumpJets>();

        // Assert
        testComponents.Count.ShouldBe(1);
        jumpJetComponents.ShouldBeEmpty();
        testComponents.First().ShouldBe(testComponent);
    }

    [Fact]
    public void CanAddComponent_WhenSlotsAreAvailable()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5, 3);
        var smallComponent = new TestComponent("Small Component");

        // Act & Assert
        sut.TryAddComponent(smallComponent).ShouldBeTrue();
    }
    
    [Fact]
    public void CanAddComponent_ChecksSlotAvailability()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5, 3);
        var smallComponent = new TestComponent("Small Component");
        var largeComponent = new TestComponent("Large Component",4);

        // Act & Assert
        sut.TryAddComponent(smallComponent,[0]).ShouldBeTrue();
        sut.TryAddComponent(largeComponent, [1,2,3,4]).ShouldBeFalse();
    }

    [Fact]
    public void GetComponentAtSlot_ReturnsCorrectComponent()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5, 6);
        var component1 = new TestComponent("Component 1",2);
        var component2 = new TestComponent("Component 2",3);
        
        sut.TryAddComponent(component1,[0,1]).ShouldBeTrue();
        sut.TryAddComponent(component2,[3,4,5]).ShouldBeTrue();

        // Act & Assert
        sut.GetComponentAtSlot(0).ShouldBe(component1);
        sut.GetComponentAtSlot(1).ShouldBe(component1);
        sut.GetComponentAtSlot(2).ShouldBeNull();
        sut.GetComponentAtSlot(3).ShouldBe(component2);
        sut.GetComponentAtSlot(4).ShouldBe(component2);
        sut.GetComponentAtSlot(5).ShouldBe(component2);
    }

    [Fact]
    public void TryAddComponent_ShouldReturnFalse_WhenNoContiguousSpaceAvailable()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5, 8);
        var fixedComponent = new TestComponent("Fixed Component",5);
        var component = new TestComponent("TestComponent", 4);

        // Act & Assert
        sut.TryAddComponent(fixedComponent).ShouldBeTrue();
        sut.TryAddComponent(component).ShouldBeFalse();
    }
    
    [Fact]
    public void TryAddComponent_ShouldOccupySlots_WhenPartiallyMounted()
    {
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5, 6);
        var comp = new TestComponent("EngineChunk", 3);
        sut.TryAddComponent(comp, [1, 3]).ShouldBeTrue(); // partial

        sut.UsedSlots.ShouldBe(2); // partial assignments still counted
        sut.GetComponentAtSlot(1).ShouldBe(comp);
        sut.GetComponentAtSlot(3).ShouldBe(comp);
        comp.IsMounted.ShouldBeFalse(); // not fully assigned yet
    }
    
    [Fact]
    public void TryAddComponent_ShouldReturnFalse_WhenSlotIsNegative()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5, 3);
        var component = new TestComponent("NegSlot");
        
        // Act
        var result = sut.TryAddComponent(component,[-1]);
        
        // Assert
        result.ShouldBeFalse();
        sut.Components.ShouldBeEmpty();
        sut.UsedSlots.ShouldBe(0);
    }
    
    [Fact]
    public void GetNextTransferLocation_ReturnsCorrectLocation()
    {
        var testUnit = UnitTests.CreateTestUnit();
        var sut = testUnit.Parts.Values.First(p=>p.Location==PartLocation.LeftArm);

        sut.GetNextTransferLocation().ShouldBe(PartLocation.CenterTorso);
    }

    [Fact]
    public void BlowOff_SetsIsBlownOffAndIsDestroyed_WhenCanBeBlownOffIsTrue()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5);
        
        // Act
        var result = sut.BlowOff();
        
        // Assert
        result.ShouldBeTrue();
        sut.IsBlownOff.ShouldBeTrue();
        sut.IsDestroyed.ShouldBeTrue();
    }
    
    [Fact]
    public void IsDestroyed_ShouldBeTrue_WhenParentPartIsDestroyed()
    {
        var testUnit = UnitTests.CreateTestUnit();
        var parent = testUnit.Parts.Values.First(p=>p.Location==PartLocation.CenterTorso);
        parent.BlowOff();
        var sut = testUnit.Parts.Values.First(p=>p.Location==PartLocation.LeftArm);

        sut.IsDestroyed.ShouldBeTrue();
    }
    
    [Fact]
    public void CriticalHit_AddsSlotToHitSlots()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5);
        
        // Act
        sut.CriticalHit(3);
        
        // Assert
        sut.HitSlots.ShouldContain(3);
        sut.HitSlots.Count.ShouldBe(1);
    }
    
    [Fact]
    public void CriticalHit_DamagesComponentInSlot()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5);
        var component = new TestComponent("Test Component");
        sut.TryAddComponent(component, [3]);
        
        // Act
        sut.CriticalHit(3);
        
        // Assert
        component.IsDestroyed.ShouldBeTrue();
        component.Hits.ShouldBe(1);
    }
    
    [Fact]
    public void CriticalHit_DoesNotDamageAlreadyDestroyedComponent()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5);
        var component = new TestComponent("Test Component");
        sut.TryAddComponent(component, [3]);
        component.Hit(); // Destroy the component first
        
        // Act
        sut.CriticalHit(3);
        
        // Assert
        component.IsDestroyed.ShouldBeTrue();
        component.Hits.ShouldBe(1); // Hits should still be 1, not 2
    }
    
    [Fact]
    public void CriticalHit_HandlesMultipleHitsToSameComponent()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5);
        var component = new TestComponent("Test Component",3);
        sut.TryAddComponent(component,[2,3,4]).ShouldBeTrue();
        
        // Act
        sut.CriticalHit(2);
        sut.CriticalHit(3);
        sut.CriticalHit(4);
        
        // Assert
        component.IsDestroyed.ShouldBeTrue();
        component.Hits.ShouldBe(1); // Component should only be hit once
        sut.HitSlots.Count.ShouldBe(3); // But all slots should be marked as hit
    }

    [Fact]
    public void ApplyDamage_ShouldRaiseArmorDamageEvent_WhenUnitIsSet()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5);
        var unit = new TestUnit();
        sut.Unit = unit;
        
        // Act
        sut.ApplyDamage(5, HitDirection.Front);
        
        // Assert
        var uiEvent = unit.DequeueNotification();
        uiEvent.ShouldNotBeNull();
        uiEvent.Type.ShouldBe(UiEventType.ArmorDamage);
        uiEvent.Parameters.Length.ShouldBe(2);
        uiEvent.Parameters[0].ShouldBe(sut.Name);
        uiEvent.Parameters[1].ShouldBe("5");
    }
    
    [Fact]
    public void ApplyDamage_ShouldRaiseStructureDamageEvent_WhenArmorIsDepletedAndStructureIsDamaged()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 5, 10);
        var unit = new TestUnit();
        sut.Unit = unit;
        
        // Act
        sut.ApplyDamage(8, HitDirection.Front); // 5 armor + 3 structure damage
        
        // Assert
        // First event should be armor damage
        var armorEvent = unit.DequeueNotification();
        armorEvent.ShouldNotBeNull();
        armorEvent.Type.ShouldBe(UiEventType.ArmorDamage);
        
        // The second event should be structure damage
        var structureEvent = unit.DequeueNotification();
        structureEvent.ShouldNotBeNull();
        structureEvent.Type.ShouldBe(UiEventType.StructureDamage);
        structureEvent.Parameters.Length.ShouldBe(2);
        structureEvent.Parameters[0].ShouldBe(sut.Name);
        structureEvent.Parameters[1].ShouldBe("3");
    }
    
    [Fact]
    public void ApplyDamage_ShouldRaiseLocationDestroyedEvent_WhenStructureIsReducedToZero()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 5, 5);
        var unit = new TestUnit();
        sut.Unit = unit;
        
        // Act
        sut.ApplyDamage(15, HitDirection.Front); // 5 armor + 5 structure + 5 excess
        
        // Assert
        // First event should be armor damage
        var armorEvent = unit.DequeueNotification();
        armorEvent.ShouldNotBeNull();
        armorEvent.Type.ShouldBe(UiEventType.ArmorDamage);
        
        // The second event should be structure damage (not checking this to keep the test focused)
        unit.DequeueNotification();
        
        // The third event should be location destroyed
        var destroyedEvent = unit.DequeueNotification();
        destroyedEvent.ShouldNotBeNull();
        destroyedEvent.Type.ShouldBe(UiEventType.LocationDestroyed);
        destroyedEvent.Parameters.Length.ShouldBe(1);
        destroyedEvent.Parameters[0].ShouldBe(sut.Name);
    }
    
    [Fact]
    public void CriticalHit_ShouldRaiseCriticalHitEvent_WhenComponentIsHit()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5);
        var unit = new TestUnit();
        sut.Unit = unit;
        
        var component = new TestComponent("Test Component");
        sut.TryAddComponent(component);
        
        // Act
        sut.CriticalHit(0); // Hit the first slot where our component is
        
        // Assert
        var criticalEvent = unit.DequeueNotification();
        criticalEvent.ShouldNotBeNull();
        criticalEvent.Type.ShouldBe(UiEventType.CriticalHit);
        criticalEvent.Parameters.Length.ShouldBe(1);
        criticalEvent.Parameters[0].ShouldBe(component.Name);
    }
    
    [Fact]
    public void CriticalHit_ShouldRaiseComponentDestroyedEvent_WhenComponentIsDestroyed()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5);
        var unit = new TestUnit();
        sut.Unit = unit;
        
        var component = new TestComponent("Test Component");
        sut.TryAddComponent(component);
        
        // Act
        sut.CriticalHit(0); // Hit the first slot where our component is
        
        // Assert
        // First event should be a critical hit
        unit.DequeueNotification();
        
        // Second event should be component destroyed
        var destroyedEvent = unit.DequeueNotification();
        destroyedEvent.ShouldNotBeNull();
        destroyedEvent.Type.ShouldBe(UiEventType.ComponentDestroyed);
        destroyedEvent.Parameters.Length.ShouldBe(1);
        destroyedEvent.Parameters[0].ShouldBe(component.Name);
    }
    
    private class TestComponent(string name, int size = 1) : Component(new EquipmentDefinition(
        name,
        MakaMekComponent.Masc,
        0,
        size));
    
    [Fact]
    public void TryAddComponent_ShouldReturnFalse_WhenSlotOutOfBounds()
    {
        // Arrange 
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5, 3); // Only 3 slots (0, 1, 2)
        var component = new TestComponent("Test Component"); 

        // Act
        var result = sut.TryAddComponent(component, [3]);

        // Assert
        result.ShouldBeFalse();
        sut.Components.ShouldBeEmpty();
        sut.UsedSlots.ShouldBe(0);
    }

    [Fact]
    public void TryAddComponent_ShouldReturnTrue_WhenAllSlotsInBounds()
    {
        // Arrange 
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5, 5); // 5 slots (0, 1, 2, 3, 4)
        var component = new TestComponent("Test Component",3); // All slots are in bounds

        // Act
        var result = sut.TryAddComponent(component,[1,2,3]);

        // Assert
        result.ShouldBeTrue();
        sut.Components.Count.ShouldBe(1);
        sut.UsedSlots.ShouldBe(3);
    }

    [Fact]
    public void FindComponentSlot_ShouldReturnNegative_WhenSizeIsNegative()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5, 3); // Only 3 slots (0, 1, 2)
        
        var method = typeof(UnitPart).GetMethod(
            "FindMountLocation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.ShouldNotBeNull();
        
        // Act
        var result = (int)method.Invoke(sut, [-1])!; 
        
        // Assert
        result.ShouldBe(-1);
    }
    
    [Fact]
    public void FindComponentSlot_ShouldReturnNegative_WhenSizeIsTooLarge()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5, 3); // Only 3 slots (0, 1, 2)
        
        var method = typeof(UnitPart).GetMethod(
            "FindMountLocation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.ShouldNotBeNull();
        
        // Act
        var result = (int)method.Invoke(sut, [sut.TotalSlots + 1])!; 
        
        // Assert
        result.ShouldBe(-1);
    }
    
    [Fact]
    public void Constructor_ShouldCreatePristinePart()
    {
        // Arrange & Act
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5);

        // Assert
        sut.IsPristine.ShouldBeTrue();
    }
    
    [Fact]
    public void ApplyArmorDamage_ShouldMakePartNotPristine()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 4, 3);
        
        // Act
        sut.ApplyDamage(1, HitDirection.Front);
        
        // Assert
        sut.IsPristine.ShouldBeFalse();
    }
    
    [Fact]
    public void ApplyStructureDamage_ShouldMakePartNotPristine()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 4, 3);
        
        // Act
        sut.ApplyDamage(5, HitDirection.Front);
        
        // Assert
        sut.IsPristine.ShouldBeFalse();
    }
    
    [Fact]
    public void BlowOff_ShouldMakePartNotPristine()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 4, 3);
        
        // Act
        sut.BlowOff();
        
        // Assert
        sut.IsPristine.ShouldBeFalse();
    }
    
    [Fact]
    public void ToData_ShouldSerializePartState()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5);
        sut.ApplyDamage(5, HitDirection.Front);
        
        // Act
        var data = sut.ToData();
        
        // Assert
        data.Location.ShouldBe(PartLocation.LeftArm);
        data.CurrentFrontArmor.ShouldBe(5);
        data.CurrentStructure.ShouldBe(5);
    }
    
    [Fact]
    public void IsWeaponConfigurationApplicable_ShouldReturnFalse_ForBaseClass()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5);
        
        // Act & Assert
        sut.IsWeaponConfigurationApplicable(WeaponConfigurationType.TorsoRotation).ShouldBeFalse();
    }

    [Fact]
    public void IsWeaponConfigurationApplicable_ShouldReturnTrue_ForNoneType()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5);
        
        // Act & Assert
        sut.IsWeaponConfigurationApplicable(WeaponConfigurationType.None).ShouldBeTrue();
    }

    public class TestUnit() : Unit("Test", "Unit", 20, [], Guid.NewGuid())
    {
        public override int CalculateBattleValue() => 0;

        public override bool CanMoveBackward(MovementType type) => true;

        public override void UpdateDestroyedStatus()
        {
            // Do nothing for tests
        }

        public override PartLocation? GetTransferLocation(PartLocation location) => location switch
        {
            PartLocation.LeftArm => PartLocation.CenterTorso,
            PartLocation.RightArm => PartLocation.CenterTorso,
            PartLocation.LeftLeg => PartLocation.CenterTorso,
            PartLocation.RightLeg => PartLocation.CenterTorso,
            _ => null
        };

        public override LocationCriticalHitsData CalculateCriticalHitsData(PartLocation location,
            IDiceRoller diceRoller,
            IDamageTransferCalculator damageTransferCalculator)
            => throw new NotImplementedException();

        public override void ApplyWeaponConfiguration(WeaponConfigurationCommand configCommand)
        {
        }

        protected override void ApplyHeatEffects()
            => throw new NotImplementedException();

        public void AddPart(UnitPart sut)
        {
            _parts[sut.Location] = sut;
            sut.Unit = this;
        }
    }
}
