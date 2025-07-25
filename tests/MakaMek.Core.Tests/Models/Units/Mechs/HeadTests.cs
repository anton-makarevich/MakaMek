using Shouldly;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Mechs;

public class HeadTests
{
    [Fact]
    public void Head_ShouldBeInitializedCorrectly()
    {
        // Arrange & Act
        var sut = new Head("Head",  8, 3);

        // Assert
        sut.Name.ShouldBe("Head");
        sut.Location.ShouldBe(PartLocation.Head);
        sut.MaxArmor.ShouldBe(8);
        sut.CurrentArmor.ShouldBe(8);
        sut.MaxStructure.ShouldBe(3);
        sut.CurrentStructure.ShouldBe(3);
        sut.TotalSlots.ShouldBe(6);
        sut.CanBeBlownOff.ShouldBeTrue();

        // Verify default components
        sut.GetComponent<LifeSupport>().ShouldNotBeNull();
        sut.GetComponent<Sensors>().ShouldNotBeNull();
        sut.GetComponent<Cockpit>().ShouldNotBeNull();
    }
    
    [Fact]
    public void BlowOff_ShouldKillPilot()
    {
        // Arrange
        var sut = new Head("Head",  8, 3);
        var mech = new Mech("Test", "TST-1A", 50, 6, [sut]);
        var pilot = new MechWarrior("John", "Doe");
        mech.AssignPilot(pilot);

        // Act
        sut.BlowOff();

        // Assert
        pilot.IsDead.ShouldBeTrue();
    }
    
    [Fact]
    public void BlowOff_ShouldNotThrow_WhenPilotIsNotAssigned()
    {
        // Arrange
        var sut = new Head("Head",  8, 3);

        // Act & Assert
        sut.BlowOff();
    }
}
