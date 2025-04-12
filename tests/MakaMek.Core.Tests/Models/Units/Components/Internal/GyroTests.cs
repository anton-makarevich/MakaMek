using Sanet.MakaMek.Core.Data.Community;
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
    }
}