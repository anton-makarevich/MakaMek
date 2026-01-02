using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units.Components;
using Shouldly;
using Sanet.MakaMek.Core.Exceptions;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

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

        public override IReadOnlyList<WeaponConfigurationOptions> GetWeaponsConfigurationOptions()
        {
            return [];
        }
    }

    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new TestComponent("Test Component");

        // Assert
        sut.Name.ShouldBe("Test Component");
        sut.IsDestroyed.ShouldBeFalse();
        sut.IsActive.ShouldBeTrue();
        sut.IsMounted.ShouldBeFalse();
    }

    [Fact]
    public void Mount_SetsIsMountedToTrue()
    {
        // Arrange
        var sut = new TestComponent("Test Component");
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);


        // Act
        sut.Mount(unitPart,[0]);

        // Assert
        sut.IsMounted.ShouldBeTrue();
        sut.MountedAtFirstLocationSlots.ShouldBe([0]);
    }

    [Fact]
    public void Mount_ShouldHandleNonConsecutiveSlots()
    {
        // Arrange
        var sut = new TestComponent("Test Component", 3);
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);


        // Act
        sut.Mount( unitPart,[2,4,5]);

        // Assert
        sut.IsMounted.ShouldBeTrue();
        sut.MountedAtFirstLocationSlots.ShouldBe([2, 4, 5]);
    }

    [Fact]
    public void Mount_ShouldThrowWhenSlotsContainDuplicates()
    {
        // Arrange
        var sut = new TestComponent("Test Component", 3);
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);

        // Act & Assert
        Should.Throw<ComponentException>(() => sut.Mount(unitPart, [2, 2, 3]))
            .Message.ShouldBe("Slot assignments cannot contain duplicates.");
    }

    [Fact]
    public void Mount_ShouldThrowWhenSlotsExceedUnitPartCapacity()
    {
        // Arrange
        var sut = new TestComponent("Test Component", 2);
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);

        // Act & Assert
        Should.Throw<ComponentException>(() => sut.Mount(unitPart, [9, 10]))
            .Message.ShouldBe("Slot assignment exceeds available slots of the unit part.");
    }

    [Fact]
    public void UnMount_ResetsMountedSlots()
    {
        // Arrange
        var sut = new TestComponent("Test Component");
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);

        sut.Mount(unitPart,[0]);

        // Act
        sut.UnMount();

        // Assert
        sut.IsMounted.ShouldBeFalse();
    }

    [Fact]
    public void UnMount_ThrowsExceptionForFixedComponents()
    {
        // Arrange
        var sut = new ShoulderActuator();
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        unitPart.TryAddComponent(sut).ShouldBeTrue();
        sut.IsMounted.ShouldBeTrue();

        // Act & Assert
        Should.Throw<ComponentException>(() => sut.UnMount())
            .Message.ShouldBe($"Shoulder is not removable");
    }

    [Fact]
    public void Hit_SetsIsDestroyedToTrue()
    {
        // Arrange
        var sut = new TestComponent("Test Component");

        // Act
        sut.Hit();

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
        sut.Hits.ShouldBe(1);
    }

    [Fact]
    public void Activate_DeactivateTogglesIsActive()
    {
        // Arrange
        var sut = new TestComponent("Test Component");
        
        // Act & Assert
        sut.IsActive.ShouldBeTrue(); // Default state
        
        sut.Deactivate();
        sut.IsActive.ShouldBeFalse();
        
        sut.Activate();
        sut.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void IsMounted_ReturnsTrueWhenMountedAtSlotsNotEmpty()
    {
        // Arrange
        var sut = new TestComponent("Test Component",2);
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        
        // Act & Assert
        sut.IsMounted.ShouldBeFalse(); // Initially not mounted
        
        sut.Mount(unitPart,[0, 1]);
        sut.IsMounted.ShouldBeTrue(); // Mounted with slots
        
        sut.UnMount();
        sut.IsMounted.ShouldBeFalse(); // Unmounted
    }

    [Fact]
    public void Mount_IgnoresIfAlreadyMounted()
    {
        // Arrange
        var sut = new TestComponent("Test Component",2);
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);

        sut.Mount(unitPart,[0, 1]);
        var initialSlots = sut.MountedAtFirstLocationSlots;

        // Act
        sut.Mount(unitPart,[2, 3]); // Try to mount again with different slots

        // Assert
        sut.MountedAtFirstLocationSlots.ShouldBeEquivalentTo(initialSlots); // Should keep original slots
    }
    
    [Fact]
    public void Mount_ComponentWithLargerSize_Throws()
    {
        // Arrange
        var sut = new TestComponent("Test Component",2);
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        
        // Act & Assert
        Should.Throw<ComponentException>(() => sut.Mount(unitPart,[2,3,4])) // Try to mount 
           .Message.ShouldBe("Component Test Component requires 2 slots.");
    }

    [Fact]
    public void UnMount_IgnoresIfNotMounted()
    {
        // Arrange
        var sut = new TestComponent("Test Component");

        // Act & Assert - should not throw
        sut.UnMount();
        sut.IsMounted.ShouldBeFalse();
    }

    [Fact]
    public void Status_ReturnsRemoved_WhenNotMounted()
    {
        var sut = new TestComponent("Test");
        sut.Status.ShouldBe(ComponentStatus.Removed);
    }
    
    [Fact]
    public void Status_ReturnsDamaged_WhenHitsLessThanHP()
    {
        var sut = new TestComponent("Test", healthPoints:2);
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        sut.Mount(unitPart,[0]);
        
        sut.Hit();
        
        sut.Status.ShouldBe(ComponentStatus.Damaged);
    }

    [Fact]
    public void Status_ReturnsDeactivated_WhenNotActive()
    {
        var sut = new TestComponent("Test");
        sut.Deactivate();
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        sut.Mount(unitPart,[0]);
        sut.Status.ShouldBe(ComponentStatus.Deactivated);
    }

    [Fact]
    public void Status_ReturnsLost_WhenMountedOnDestroyed()
    {
        var sut = new TestComponent("Test");
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        sut.Mount(unitPart,[0]);
        unitPart.ApplyDamage(20, HitDirection.Front, true);
        sut.Status.ShouldBe(ComponentStatus.Lost);
    }

    [Fact]
    public void Status_ReturnsActive_WhenAllOk()
    {
        var sut = new TestComponent("Test");
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        sut.Mount(unitPart, [0]);
        sut.Status.ShouldBe(ComponentStatus.Active);
    }
    
    [Fact]
    public void Mount_WithUnitPart_ShouldSetMountedOnProperty()
    {
        // Arrange
        var sut = new TestComponent("Test Component", 2);
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        
        // Act
        sut.Mount(unitPart, [0, 1]);
        
        // Assert
        sut.FirstMountPart.ShouldBe(unitPart);
        sut.FirstMountPartLocation.ShouldBe(PartLocation.LeftArm);
        sut.MountedOn.ShouldContain(unitPart);
    }

    [Fact]
    public void Mount_ShouldNotMountComponent_WhenSlotsAreEmpty()
    {
        // Arrange
        var sut = new TestComponent("Test Component", 1);
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        
        // Act
        sut.Mount(unitPart, []);
        
        // Assert
        sut.IsMounted.ShouldBeFalse();
    }
    
    [Fact]
    public void UnMount_ShouldClearMountedOnProperty()
    {
        // Arrange
        var sut = new TestComponent("Test Component", 2);
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        sut.Mount(unitPart,[0, 1]);
        
        // Act
        sut.UnMount();
        
        // Assert
        sut.MountedOn.ShouldBeEmpty();
        sut.FirstMountPartLocation.ShouldBeNull();
    }
    
    [Fact]
    public void TryAddComponent_ShouldSetComponentLocation()
    {
        // Arrange
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        var sut = new TestComponent("Test Component", 2);
        
        // Act
        var result = unitPart.TryAddComponent(sut);
        
        // Assert
        result.ShouldBeTrue();
        sut.MountedOn.ShouldContain(unitPart);
        sut.FirstMountPartLocation.ShouldBe(PartLocation.LeftArm);
    }
    
    [Fact]
    public void RemoveComponent_ShouldUnmountComponent()
    {
        // Arrange
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        var sut = new TestComponent("Test Component", 2);
        unitPart.TryAddComponent(sut).ShouldBeTrue();
        sut.IsMounted.ShouldBeTrue();
        
        // Act
        var result = unitPart.RemoveComponent(sut);
        
        // Assert
        result.ShouldBeTrue();
        sut.IsMounted.ShouldBeFalse();
        sut.MountedOn.ShouldBeEmpty();
        unitPart.Components.ShouldNotContain(sut);
    }
    
    [Fact]
    public void RemoveComponent_ShouldReturnFalse_WhenComponentNotInPart()
    {
        // Arrange
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        var sut = new TestComponent("Test Component", 2);
        
        // Act
        var result = unitPart.RemoveComponent(sut);
        
        // Assert
        result.ShouldBeFalse();
    }
    
    [Fact]
    public void Component_ShouldHaveCorrectLocation_WhenAddedToPart()
    {
        // Arrange
        var unitPart = new TestUnitPart("Test Part", PartLocation.LeftArm, 10, 5, 10);
        var sut = new TestComponent("Fixed Component", 2);
        
        // Act
        var result = unitPart.TryAddComponent(sut);
        
        // Assert
        result.ShouldBeTrue();
        sut.FirstMountPart.ShouldBe(unitPart);
        sut.FirstMountPartLocation.ShouldBe(PartLocation.LeftArm);
    }
    
    [Fact]
    public void CanExplode_DefaultIsFalse()
    {
        // Arrange
        var sut = new TestComponent("Test Component");
        
        // Act & Assert
        sut.CanExplode.ShouldBeFalse();
    }
    
    [Fact]
    public void GetExplosionDamage_DefaultReturnsZero()
    {
        // Arrange
        var sut = new TestComponent("Test Component");
        
        // Act
        var damage = sut.GetExplosionDamage();
        
        // Assert
        damage.ShouldBe(0);
    }
    
    [Fact]
    public void HasExploded_DefaultIsFalse()
    {
        // Arrange
        var sut = new TestComponent("Test Component");
        
        // Act & Assert
        sut.HasExploded.ShouldBeFalse();
    }
    
    [Fact]
    public void Hit_DoesNotChangeHasExploded()
    {
        // Arrange
        var sut = new TestComponent("Test Component");
        
        // Act
        sut.Hit();
        
        // Assert
        sut.HasExploded.ShouldBeFalse();
        sut.IsDestroyed.ShouldBeTrue();
    }
}
