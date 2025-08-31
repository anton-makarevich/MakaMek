using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Events;
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
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5, 12);

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
        var sut = new TestUnitPart(PartLocation.LeftArm, maxArmor, maxStructure, 12);

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
        var part = new TestUnitPart(PartLocation.LeftArm, 0, 5, 12);
        var masc = new TestComponent("Test MASC", [0, 1]);
        part.TryAddComponent(masc);

        // Act
        part.ApplyDamage(10, HitDirection.Front); // Ensure the structure is destroyed

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
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5, 3);
        var largeComponent = new TestComponent("Large Component", [0, 1, 2, 3]);

        // Act & Assert
        sut.TryAddComponent(largeComponent).ShouldBeFalse();
    }
    
    [Fact]
    public void CanAddComponent_ChecksSlotAvailability()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5, 3);
        var smallComponent = new TestComponent("Small Component", [0, 1]);
        var largeComponent = new TestComponent("Large Component", [0, 1, 2, 3]);

        // Act & Assert
        sut.TryAddComponent(smallComponent);
        sut.TryAddComponent(largeComponent).ShouldBeFalse();
        sut.TryAddComponent(new TestComponent("Not fixed Component", [])).ShouldBeTrue();
        sut.TryAddComponent(new TestComponent("Not fixed Component 2", [])).ShouldBeFalse();
    }

    [Fact]
    public void GetComponentAtSlot_ReturnsCorrectComponent()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5, 6);
        var component1 = new TestComponent("Component 1", [0, 1]);
        var component2 = new TestComponent("Component 2", [3, 4, 5]);
        
        sut.TryAddComponent(component1).ShouldBeTrue();
        sut.TryAddComponent(component2).ShouldBeTrue();

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
        var fixedComponent = new TestComponent("Fixed Component", [2,3,4,5]);
        var component = new TestComponent("TestComponent", [], 4);

        // Act & Assert
        sut.TryAddComponent(fixedComponent).ShouldBeTrue();
        sut.TryAddComponent(component).ShouldBeFalse();
    }
    
    [Fact]
    public void TryAddComponent_ShouldReturnFalse_WhenSlotIsNegative()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5, 3);
        var component = new TestComponent("NegSlot", [-1]);
        
        // Act
        var result = sut.TryAddComponent(component);
        
        // Assert
        result.ShouldBeFalse();
        sut.Components.ShouldBeEmpty();
        sut.UsedSlots.ShouldBe(0);
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
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5, 12);
        
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
        var parent = testUnit.Parts.First(p=>p.Location==PartLocation.CenterTorso);
        parent.BlowOff();
        var sut = testUnit.Parts.First(p=>p.Location==PartLocation.LeftArm);

        sut.IsDestroyed.ShouldBeTrue();
    }
    
    [Fact]
    public void CriticalHit_AddsSlotToHitSlots()
    {
        // Arrange
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5, 12);
        
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
        var sut = new TestUnitPart(PartLocation.LeftArm, 10, 5, 12);
        var component = new TestComponent("Test Component", [2, 3, 4]);
        sut.TryAddComponent(component);
        
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
        part.ApplyDamage(5, HitDirection.Front);
        
        // Assert
        var uiEvent = unit.DequeueNotification();
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
        part.ApplyDamage(8, HitDirection.Front); // 5 armor + 3 structure damage
        
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
        part.ApplyDamage(15, HitDirection.Front); // 5 armor + 5 structure + 5 excess
        
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
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 12);
        var unit = new TestUnit();
        part.Unit = unit;
        
        var component = new TestComponent("Test Component",[]);
        part.TryAddComponent(component);
        
        // Act
        part.CriticalHit(0); // Hit the first slot where our component is
        
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
    
    private class TestComponent(string name, int[] slots, int size = 1) : Component(name, slots, size)
    {
        public override MakaMekComponent ComponentType => throw new NotImplementedException();
    }
    
    [Fact]
    public void TryAddComponent_ShouldReturnFalse_WhenSlotOutOfBounds()
    {
        // Arrange - This tests line 73 (slot bounds checking)
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 3); // Only 3 slots (0, 1, 2)
        var component = new TestComponent("Test Component", [3]); // Slot 3 is out of bounds

        // Act
        var result = part.TryAddComponent(component);

        // Assert
        result.ShouldBeFalse();
        part.Components.ShouldBeEmpty();
        part.UsedSlots.ShouldBe(0);
    }

    [Fact]
    public void TryAddComponent_ShouldReturnFalse_WhenMultipleSlotsOutOfBounds()
    {
        // Arrange - This tests line 73 (slot bounds checking with multiple slots)
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 3); // Only 3 slots (0, 1, 2)
        var component = new TestComponent("Test Component", [1, 2, 3, 4]); // Slots 3 and 4 are out of bounds

        // Act
        var result = part.TryAddComponent(component);

        // Assert
        result.ShouldBeFalse();
        part.Components.ShouldBeEmpty();
        part.UsedSlots.ShouldBe(0);
    }

    [Fact]
    public void TryAddComponent_ShouldReturnTrue_WhenAllSlotsInBounds()
    {
        // Arrange - This verifies line 73 passes when slots are valid
        var part = new TestUnitPart(PartLocation.LeftArm, 10, 5, 5); // 5 slots (0, 1, 2, 3, 4)
        var component = new TestComponent("Test Component", [2, 3, 4]); // All slots are in bounds

        // Act
        var result = part.TryAddComponent(component);

        // Assert
        result.ShouldBeTrue();
        part.Components.Count.ShouldBe(1);
        part.UsedSlots.ShouldBe(3);
    }

    public class TestUnit() : Unit("Test", "Unit", 20, 4, [], Guid.NewGuid())
    {
        public override int CalculateBattleValue() => 0;

        public override bool CanMoveBackward(MovementType type) => true;

        protected override void UpdateDestroyedStatus()
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

        public override LocationCriticalHitsData CalculateCriticalHitsData(PartLocation location, IDiceRoller diceRoller)
            => throw new NotImplementedException();

        protected override void ApplyHeatEffects()
            => throw new NotImplementedException();

        public void AddPart(UnitPart part)
        {
            _parts.Add(part);
            part.Unit = this;
        }
    }
}
