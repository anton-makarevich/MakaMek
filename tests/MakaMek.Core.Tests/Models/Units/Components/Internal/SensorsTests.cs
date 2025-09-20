using Sanet.MakaMek.Core.Data.Units.Components;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Internal;

public class SensorsTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Sensors();

        // Assert
        sut.Name.ShouldBe("Sensors");
        sut.MountedAtSlots.ToList().Count.ShouldBe(2);
        sut.MountedAtSlots.ShouldBe([1,4]);
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.Sensors);
        sut.IsRemovable.ShouldBeFalse();
        sut.HealthPoints.ShouldBe(2);
    }
    
    [Fact]
    public void FirstHit_DoesNotDestroyComponent()
    {
        var sut = new Sensors();
        
        sut.Hit();
        
        sut.IsDestroyed.ShouldBeFalse();
        sut.Hits.ShouldBe(1);;
    }
    
    [Fact]
    public void SecondHit_DoesDestroyComponent()
    {
        var sut = new Sensors();
        
        sut.Hit();
        sut.Hit();
        
        sut.IsDestroyed.ShouldBeTrue();
        sut.Hits.ShouldBe(2);
    }
}