using Sanet.MakaMek.Core.Data.Units.Components;
using Shouldly;
using Sanet.MakaMek.Core.Exceptions;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components;

public class ComponentTests
{
    private class TestComponent(string name, int size = 1, int healthPoints = 1) : Component(new EquipmentDefinition(
        name,
        MakaMekComponent.Masc,
        0,
        size,
        healthPoints));
    
    private class TestUnitPart(string name, PartLocation location, int maxArmor, int maxStructure, int slots)
        : UnitPart(name, location, maxArmor, maxStructure, slots)
    {
        internal override bool CanBeBlownOff => true;
    }

    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var component = new TestComponent("Test Component");

        // Assert
        component.Name.ShouldBe("Test Component");
        component.IsDestroyed.ShouldBeFalse();
        component.IsActive.ShouldBeTrue();
        component.IsMounted.ShouldBeFalse();
    }

    [Fact]
    public void Mount_SetsIsMountedToTrue()
    {
        // Arrange
        var component = new TestComponent("Test Component");
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);


        // Act
        component.Mount([0], unitPart);

        // Assert
        component.IsMounted.ShouldBeTrue();
    }

    [Fact]
    public void UnMount_ResetsMountedSlots()
    {
        // Arrange
        var component = new TestComponent("Test Component");
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);

        component.Mount([0],unitPart);

        // Act
        component.UnMount();

        // Assert
        component.IsMounted.ShouldBeFalse();
    }

    [Fact]
    public void UnMount_ThrowsExceptionForFixedComponents()
    {
        // Arrange
        var component = new TestComponent("Fixed Component");

        // Act & Assert
        var exception = Assert.Throws<ComponentException>(() => component.UnMount());
        exception.Message.ShouldBe("Fixed components cannot be unmounted.");
    }

    [Fact]
    public void Hit_SetsIsDestroyedToTrue()
    {
        // Arrange
        var component = new TestComponent("Test Component");

        // Act
        component.Hit();

        // Assert
        component.IsDestroyed.ShouldBeTrue();
        component.Hits.ShouldBe(1);
    }

    [Fact]
    public void Activate_DeactivateTogglesIsActive()
    {
        // Arrange
        var component = new TestComponent("Test Component");
        
        // Act & Assert
        component.IsActive.ShouldBeTrue(); // Default state
        
        component.Deactivate();
        component.IsActive.ShouldBeFalse();
        
        component.Activate();
        component.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void IsMounted_ReturnsTrueWhenMountedAtSlotsNotEmpty()
    {
        // Arrange
        var component = new TestComponent("Test Component",2);
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        
        // Act & Assert
        component.IsMounted.ShouldBeFalse(); // Initially not mounted
        
        component.Mount([0, 1],unitPart);
        component.IsMounted.ShouldBeTrue(); // Mounted with slots
        
        component.UnMount();
        component.IsMounted.ShouldBeFalse(); // Unmounted
    }

    [Fact]
    public void Mount_IgnoresIfAlreadyMounted()
    {
        // Arrange
        var component = new TestComponent("Test Component",2);
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);

        component.Mount([0, 1],unitPart);
        var initialSlots = component.MountedAtSlots;

        // Act
        component.Mount([2, 3],unitPart); // Try to mount again with different slots

        // Assert
        component.MountedAtSlots.ShouldBeEquivalentTo(initialSlots); // Should keep original slots
    }
    
    [Fact]
    public void Mount_ComponentWithWrongSize_Throws()
    {
        // Arrange
        var component = new TestComponent("Test Component",2);
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);

        
        // Act & Assert
        var exception = Assert.Throws<ComponentException>(() => component.Mount([2],unitPart));// Try to mount 
        exception.Message.ShouldBe("Component Test Component requires 2 slots.");
        
    }

    [Fact]
    public void UnMount_IgnoresIfNotMounted()
    {
        // Arrange
        var component = new TestComponent("Test Component");

        // Act & Assert - should not throw
        component.UnMount();
        component.IsMounted.ShouldBeFalse();
    }

    [Fact]
    public void Status_ReturnsRemoved_WhenNotMounted()
    {
        var component = new TestComponent("Test");
        component.Status.ShouldBe(ComponentStatus.Removed);
    }
    
    [Fact]
    public void Status_ReturnsDamaged_WhenHitsLessThanHP()
    {
        var component = new TestComponent("Test", healthPoints:2);
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        component.Mount([0], unitPart);
        
        component.Hit();
        
        component.Status.ShouldBe(ComponentStatus.Damaged);
    }

    [Fact]
    public void Status_ReturnsDeactivated_WhenNotActive()
    {
        var component = new TestComponent("Test");
        typeof(Component).GetProperty("IsActive")!.SetValue(component, false);
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        component.Mount([0], unitPart);
        component.Status.ShouldBe(ComponentStatus.Deactivated);
    }

    [Fact]
    public void Status_ReturnsLost_WhenMountedOnDestroyed()
    {
        var component = new TestComponent("Test");
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        component.Mount([0], unitPart);
        unitPart.ApplyDamage(20, HitDirection.Front, true);
        component.Status.ShouldBe(ComponentStatus.Lost);
    }

    [Fact]
    public void Status_ReturnsActive_WhenAllOk()
    {
        var component = new TestComponent("Test");
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        component.Mount([0], unitPart);
        component.Status.ShouldBe(ComponentStatus.Active);
    }
    
    [Fact]
    public void Mount_WithUnitPart_ShouldSetMountedOnProperty()
    {
        // Arrange
        var component = new TestComponent("Test Component", 2);
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        
        // Act
        component.Mount([0, 1], unitPart);
        
        // Assert
        component.GetPrimaryMountLocation().ShouldBe(unitPart);
        component.GetLocation().ShouldBe(PartLocation.LeftArm);
    }
    
    [Fact]
    public void UnMount_ShouldClearMountedOnProperty()
    {
        // Arrange
        var component = new TestComponent("Test Component", 2);
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        component.Mount([0, 1], unitPart);
        
        // Act
        component.UnMount();
        
        // Assert
        component.MountedOn.ShouldBeNull();
        component.GetLocation().ShouldBeNull();
    }
    
    [Fact]
    public void TryAddComponent_ShouldSetComponentLocation()
    {
        // Arrange
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        var component = new TestComponent("Test Component", 2);
        
        // Act
        var result = unitPart.TryAddComponent(component);
        
        // Assert
        result.ShouldBeTrue();
        component.MountedOn.ShouldContain(unitPart);
        component.GetLocation().ShouldBe(PartLocation.LeftArm);
    }
    
    [Fact]
    public void RemoveComponent_ShouldUnmountComponent()
    {
        // Arrange
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        var component = new TestComponent("Test Component", 2);
        unitPart.TryAddComponent(component);
        
        // Act
        var result = unitPart.RemoveComponent(component);
        
        // Assert
        result.ShouldBeTrue();
        component.IsMounted.ShouldBeFalse();
        component.MountedOn.ShouldBeNull();
        unitPart.Components.ShouldNotContain(component);
    }
    
    [Fact]
    public void RemoveComponent_ShouldReturnFalse_WhenComponentNotInPart()
    {
        // Arrange
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        var component = new TestComponent("Test Component", 2);
        
        // Act
        var result = unitPart.RemoveComponent(component);
        
        // Assert
        result.ShouldBeFalse();
    }
    
    [Fact]
    public void FixedComponent_ShouldHaveCorrectLocation()
    {
        // Arrange
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        var fixedComponent = new TestComponent("Fixed Component", 2);
        
        // Act
        var result = unitPart.TryAddComponent(fixedComponent);
        
        // Assert
        result.ShouldBeTrue();
        fixedComponent.GetPrimaryMountLocation().ShouldBe(unitPart);
        fixedComponent.GetLocation().ShouldBe(PartLocation.LeftArm);
    }
    
    [Fact]
    public void CanExplode_DefaultIsFalse()
    {
        // Arrange
        var component = new TestComponent("Test Component");
        
        // Act & Assert
        component.CanExplode.ShouldBeFalse();
    }
    
    [Fact]
    public void GetExplosionDamage_DefaultReturnsZero()
    {
        // Arrange
        var component = new TestComponent("Test Component");
        
        // Act
        var damage = component.GetExplosionDamage();
        
        // Assert
        damage.ShouldBe(0);
    }
    
    [Fact]
    public void HasExploded_DefaultIsFalse()
    {
        // Arrange
        var component = new TestComponent("Test Component");
        
        // Act & Assert
        component.HasExploded.ShouldBeFalse();
    }
    
    [Fact]
    public void Hit_DoesNotChangeHasExploded()
    {
        // Arrange
        var component = new TestComponent("Test Component");
        
        // Act
        component.Hit();
        
        // Assert
        component.HasExploded.ShouldBeFalse();
        component.IsDestroyed.ShouldBeTrue();
    }
}
