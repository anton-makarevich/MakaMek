using Sanet.MakaMek.Core.Data.Community;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;

namespace Sanet.MakaMek.Core.Tests.Models.Units;

public class UnitPartTests
{
    private class TestUnitPart(PartLocation location, int maxArmor, int maxStructure, int slots)
        : UnitPart("Test", location, maxArmor, maxStructure, slots);

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
        part.ApplyDamage(10); // Ensure structure is destroyed

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

    private class TestComponent(string name, int[] slots, int size = 1) : Component(name, slots, size)
    {
        public override MakaMekComponent ComponentType => throw new NotImplementedException();
    }
}
