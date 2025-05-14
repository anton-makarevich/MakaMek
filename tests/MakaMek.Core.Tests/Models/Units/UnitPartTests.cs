using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;

namespace Sanet.MakaMek.Core.Tests.Models.Units;

public class UnitPartTests
{
    private class TestUnitPart(PartLocation location, int maxArmor, int maxStructure, int slots)
        : UnitPart("Test", location, maxArmor, maxStructure, slots)
    {
        internal override bool CanBeBlownOff =>true;
    }

    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 12);

        // Assert
        part.Location.ShouldBe(PartLocation.LeftArm);
        part.MaxArmor.ShouldBe(10);
        part.CurrentArmor.ShouldBe(10);
        part.MaxStructure.ShouldBe(5);
        part.CurrentStructure.ShouldBe(5);
        part.TotalSlots.ShouldBe(12);
        part.UsedSlots.ShouldBe(0);
        part.AvailableSlots.ShouldBe(12);
        part.Components.ShouldBeEmpty();
        part.IsDestroyed.ShouldBeFalse();
        part.GetNextTransferLocation().ShouldBeNull();
        part.HitSlots.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(5, 10, 5, 0)] // Damage does not exceed armor
    [InlineData(10, 10, 5, 0)] // Damage does exceed armor but structure remains
    [InlineData(20, 10, 5, 5)] // Damage exceeds armor and structure
    public void ApplyDamage_HandlesArmor(int damage, int maxArmor, int maxStructure, int expectedExcess)
    {
        // Arrange
        var part = new TestUnitPart(PartLocation.LeftArm, maxArmor, maxStructure, 12);

        // Act
        var excessDamage = part.ApplyDamage(damage);

        // Assert
        excessDamage.ShouldBe(expectedExcess);
        
        if (damage <= maxArmor)
        {
            part.CurrentArmor.ShouldBe(maxArmor - damage);
            part.CurrentStructure.ShouldBe(maxStructure);
            part.IsDestroyed.ShouldBeFalse();
        }
        else if (damage < maxArmor + maxStructure)
        {
            part.CurrentArmor.ShouldBe(0);
            part.CurrentStructure.ShouldBe(maxStructure - (damage - maxArmor));
            part.IsDestroyed.ShouldBeFalse();
        }
    }
    

    [Fact]
    public void ApplyDamage_DoesNotDestroyComponentsWhenStructureIsDestroyed()
    {
        // Arrange
        var part = new TestUnitPart(PartLocation.LeftArm, 0, 5, 12);
        var masc = new TestComponent("Test MASC", [0, 1]);
        part.TryAddComponent(masc);

        // Act
        part.ApplyDamage(10); // Ensure the structure is destroyed

        // Assert
        part.IsDestroyed.ShouldBeTrue();
        masc.IsDestroyed.ShouldBeFalse(); // Component should not be automatically destroyed
        masc.IsAvailable.ShouldBeFalse(); // not destroyed but not available to be used
        masc.IsActive.ShouldBeTrue(); // Components start active by default
    }

    [Fact]
    public void GetComponents_ReturnsCorrectComponentTypes()
    {
        // Arrange
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 12);
        var testComponent = new TestComponent("Test Component", [0, 1]);
        part.TryAddComponent(testComponent);

        // Act
        var testComponents = part.GetComponents<TestComponent>().ToList();
        var jumpJetComponents = part.GetComponents<JumpJets>();

        // Assert
        testComponents.Count.ShouldBe(1);
        jumpJetComponents.ShouldBeEmpty();
        testComponents.First().ShouldBe(testComponent);
    }

    [Fact]
    public void TryAddComponent_RespectsSlotLimits()
    {
        // Arrange
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 3);
        var smallComponent = new TestComponent("Small Component", [0, 1]);
        var largeComponent = new TestComponent("Large Component", [0, 1, 2, 3]);

        // Act & Assert
        part.TryAddComponent(smallComponent).ShouldBeTrue();
        part.UsedSlots.ShouldBe(2);
        part.AvailableSlots.ShouldBe(1);
        
        part.TryAddComponent(largeComponent).ShouldBeFalse();
        part.Components.Count.ShouldBe(1);
        part.UsedSlots.ShouldBe(2);
    }

    [Fact]
    public void CanAddFixedComponent_WhenSlotsAreAvailable()
    {
        // Arrange
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 3);
        var smallComponent = new TestComponent("Small Component", [0, 1]);

        // Act & Assert
        part.TryAddComponent(smallComponent).ShouldBeTrue();
    }
    
    [Fact]
    public void CannotAddFixedComponent_WhenSlotsAreNotAvailable()
    {
        // Arrange
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 3);
        var largeComponent = new TestComponent("Large Component", [0, 1, 2, 3]);

        // Act & Assert
        part.TryAddComponent(largeComponent).ShouldBeFalse();
    }
    
    [Fact]
    public void CanAddComponent_ChecksSlotAvailability()
    {
        // Arrange
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 3);
        var smallComponent = new TestComponent("Small Component", [0, 1]);
        var largeComponent = new TestComponent("Large Component", [0, 1, 2, 3]);

        // Act & Assert
        part.TryAddComponent(smallComponent);
        part.TryAddComponent(largeComponent).ShouldBeFalse();
        part.TryAddComponent(new TestComponent("Not fixed Component", [])).ShouldBeTrue();
        part.TryAddComponent(new TestComponent("Not fixed Component 2", [])).ShouldBeFalse();
    }

    [Fact]
    public void GetComponentAtSlot_ReturnsCorrectComponent()
    {
        // Arrange
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 6);
        var component1 = new TestComponent("Component 1", [0, 1]);
        var component2 = new TestComponent("Component 2", [3, 4, 5]);
        
        part.TryAddComponent(component1);
        part.TryAddComponent(component2);

        // Act & Assert
        part.GetComponentAtSlot(0).ShouldBe(component1);
        part.GetComponentAtSlot(1).ShouldBe(component1);
        part.GetComponentAtSlot(2).ShouldBeNull();
        part.GetComponentAtSlot(3).ShouldBe(component2);
        part.GetComponentAtSlot(4).ShouldBe(component2);
        part.GetComponentAtSlot(5).ShouldBe(component2);
    }

    [Fact]
    public void FindMountLocation_ReturnsCorrectSlotForComponentSize()
    {
        // Arrange
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 8);
        var fixedComponent = new TestComponent("Fixed Component", [2,3,4,5]);
        var component = new TestComponent("TestComponent", [], 4);

        // Act & Assert
        part.TryAddComponent(fixedComponent).ShouldBeTrue();
        part.TryAddComponent(component).ShouldBeFalse();
    }
    
    [Fact]
    public void GetNextTransferLocation_ReturnsCorrectLocation()
    {
        var testUnit = UnitTests.CreateTestUnit();
        var sut = testUnit.Parts.First(p=>p.Location==PartLocation.LeftArm);

        sut.GetNextTransferLocation().ShouldBe(PartLocation.CenterTorso);
    }

    [Fact]
    public void BlowOff_SetsIsBlownOffAndIsDestroyed_WhenCanBeBlownOffIsTrue()
    {
        // Arrange
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 12);
        
        // Act
        var result = part.BlowOff();
        
        // Assert
        result.ShouldBeTrue();
        part.IsBlownOff.ShouldBeTrue();
        part.IsDestroyed.ShouldBeTrue();
    }
    
    [Fact]
    public void IsDestroyed_ShouldBeTrue_WhenParenPartIsDestroyed()
    {
        var testUnit = UnitTests.CreateTestUnit();
        var parent = testUnit.Parts.First(p=>p.Location==PartLocation.CenterTorso);
        parent.BlowOff();
        var sut = testUnit.Parts.First(p=>p.Location==PartLocation.LeftArm);

        sut.IsDestroyed.ShouldBeTrue();
    }
    
    [Fact]
    public void CriticalHit_AddsSlotToHitSlots()
    {
        // Arrange
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 12);
        
        // Act
        part.CriticalHit(3);
        
        // Assert
        part.HitSlots.ShouldContain(3);
        part.HitSlots.Count.ShouldBe(1);
    }
    
    [Fact]
    public void CriticalHit_DamagesComponentInSlot()
    {
        // Arrange
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 12);
        var component = new TestComponent("Test Component", [2, 3, 4]);
        part.TryAddComponent(component);
        
        // Act
        part.CriticalHit(3);
        
        // Assert
        component.IsDestroyed.ShouldBeTrue();
        component.Hits.ShouldBe(1);
    }
    
    [Fact]
    public void CriticalHit_DoesNotDamageAlreadyDestroyedComponent()
    {
        // Arrange
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 12);
        var component = new TestComponent("Test Component", [2, 3, 4]);
        part.TryAddComponent(component);
        component.Hit(); // Destroy the component first
        
        // Act
        part.CriticalHit(3);
        
        // Assert
        component.IsDestroyed.ShouldBeTrue();
        component.Hits.ShouldBe(1); // Hits should still be 1, not 2
    }
    
    [Fact]
    public void CriticalHit_HandlesMultipleHitsToSameComponent()
    {
        // Arrange
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 12);
        var component = new TestComponent("Test Component", [2, 3, 4]);
        part.TryAddComponent(component);
        
        // Act
        part.CriticalHit(2);
        part.CriticalHit(3);
        part.CriticalHit(4);
        
        // Assert
        component.IsDestroyed.ShouldBeTrue();
        component.Hits.ShouldBe(1); // Component should only be hit once
        part.HitSlots.Count.ShouldBe(3); // But all slots should be marked as hit
    }

    [Fact]
    public void ApplyDamage_ShouldRaiseArmorDamageEvent_WhenUnitIsSet()
    {
        // Arrange
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 12);
        var unit = new TestUnit();
        part.Unit = unit;
        
        // Act
        part.ApplyDamage(5);
        
        // Assert
        var uiEvent = unit.DequeueEvent();
        uiEvent.ShouldNotBeNull();
        uiEvent.Type.ShouldBe(UiEventType.ArmorDamage);
        uiEvent.Parameters.Length.ShouldBe(2);
        uiEvent.Parameters[0].ShouldBe(part.Name);
        uiEvent.Parameters[1].ShouldBe("5");
    }
    
    [Fact]
    public void ApplyDamage_ShouldRaiseStructureDamageEvent_WhenArmorIsDepletedAndStructureIsDamaged()
    {
        // Arrange
        var part = new TestUnitPart(PartLocation.LeftArm, 5, 10, 12);
        var unit = new TestUnit();
        part.Unit = unit;
        
        // Act
        part.ApplyDamage(8); // 5 armor + 3 structure damage
        
        // Assert
        // First event should be armor damage
        var armorEvent = unit.DequeueEvent();
        armorEvent.ShouldNotBeNull();
        armorEvent.Type.ShouldBe(UiEventType.ArmorDamage);
        
        // The second event should be structure damage
        var structureEvent = unit.DequeueEvent();
        structureEvent.ShouldNotBeNull();
        structureEvent.Type.ShouldBe(UiEventType.StructureDamage);
        structureEvent.Parameters.Length.ShouldBe(2);
        structureEvent.Parameters[0].ShouldBe(part.Name);
        structureEvent.Parameters[1].ShouldBe("3");
    }
    
    [Fact]
    public void ApplyDamage_ShouldRaiseLocationDestroyedEvent_WhenStructureIsReducedToZero()
    {
        // Arrange
        var part = new TestUnitPart(PartLocation.LeftArm, 5, 5, 12);
        var unit = new TestUnit();
        part.Unit = unit;
        
        // Act
        part.ApplyDamage(15); // 5 armor + 5 structure + 5 excess
        
        // Assert
        // First event should be armor damage
        var armorEvent = unit.DequeueEvent();
        armorEvent.ShouldNotBeNull();
        armorEvent.Type.ShouldBe(UiEventType.ArmorDamage);
        
        // The second event should be structure damage (not checking this to keep the test focused)
        unit.DequeueEvent();
        
        // The third event should be location destroyed
        var destroyedEvent = unit.DequeueEvent();
        destroyedEvent.ShouldNotBeNull();
        destroyedEvent.Type.ShouldBe(UiEventType.LocationDestroyed);
        destroyedEvent.Parameters.Length.ShouldBe(1);
        destroyedEvent.Parameters[0].ShouldBe(part.Name);
    }
    
    [Fact]
    public void CriticalHit_ShouldRaiseCriticalHitEvent_WhenComponentIsHit()
    {
        // Arrange
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 12);
        var unit = new TestUnit();
        part.Unit = unit;
        
        var component = new TestComponent("Test Component",[]);
        part.TryAddComponent(component);
        
        // Act
        part.CriticalHit(0); // Hit the first slot where our component is
        
        // Assert
        var criticalEvent = unit.DequeueEvent();
        criticalEvent.ShouldNotBeNull();
        criticalEvent.Type.ShouldBe(UiEventType.CriticalHit);
        criticalEvent.Parameters.Length.ShouldBe(1);
        criticalEvent.Parameters[0].ShouldBe(component.Name);
    }
    
    [Fact]
    public void CriticalHit_ShouldRaiseComponentDestroyedEvent_WhenComponentIsDestroyed()
    {
        // Arrange
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 12);
        var unit = new TestUnit();
        part.Unit = unit;
        
        var component = new TestComponent("Test Component",[]);
        part.TryAddComponent(component);
        
        // Act
        part.CriticalHit(0); // Hit the first slot where our component is
        
        // Assert
        // First event should be a critical hit
        unit.DequeueEvent();
        
        // Second event should be component destroyed
        var destroyedEvent = unit.DequeueEvent();
        destroyedEvent.ShouldNotBeNull();
        destroyedEvent.Type.ShouldBe(UiEventType.ComponentDestroyed);
        destroyedEvent.Parameters.Length.ShouldBe(1);
        destroyedEvent.Parameters[0].ShouldBe(component.Name);
    }
    
    private class TestComponent(string name, int[] slots, int size = 1) : Component(name, slots, size)
    {
        public override MakaMekComponent ComponentType => throw new NotImplementedException();
    }
    
    private class TestUnit() : Unit("Test", "Unit", 20, 4, [], Guid.NewGuid())
    {
        public override int CalculateBattleValue() => 0;
        
        public override bool CanMoveBackward(MovementType type) => true;
        
        public override PartLocation? GetTransferLocation(PartLocation location) => null;
        
        public override LocationCriticalHitsData CalculateCriticalHitsData(PartLocation location, IDiceRoller diceRoller)
            => throw new NotImplementedException();
        
        protected override void ApplyHeatEffects()
            => throw new NotImplementedException();
    }
}
