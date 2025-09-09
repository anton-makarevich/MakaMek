using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Internal;

public class GyroTests
{

    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Gyro();

        // Assert
        sut.Name.ShouldBe("Gyro");
        sut.MountedAtSlots.ToList().Count.ShouldBe(4);
        sut.MountedAtSlots.ShouldBe([3, 4, 5, 6]);
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.Gyro);
        sut.IsRemovable.ShouldBeFalse();
        sut.HealthPoints.ShouldBe(2);
    }

    [Fact]
    public void FirstHit_DoesNotDestroyComponent()
    {
        var sut = new Gyro();
        
        sut.Hit();
        
        sut.IsDestroyed.ShouldBeFalse();
        sut.Hits.ShouldBe(1);;
    }
    
    [Fact]
    public void SecondHit_DoesDestroyComponent()
    {
        var sut = new Gyro();
        
        sut.Hit();
        sut.Hit();
        
        sut.IsDestroyed.ShouldBeTrue();
        sut.Hits.ShouldBe(2);
    }
}