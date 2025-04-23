using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Mechs;

public class ArmTests
{
    [Fact]
    public void Arm_ShouldBeInitializedCorrectly()
    {
        var leftArm = new Arm("Left Arm", PartLocation.LeftArm, 4, 3);
        var rightArm = new Arm("Right Arm", PartLocation.RightArm, 4, 3);

        leftArm.Location.ShouldBe(PartLocation.LeftArm);
        leftArm.MaxArmor.ShouldBe(4);
        leftArm.MaxStructure.ShouldBe(3);
        leftArm.CanBeBlownOff.ShouldBeTrue();
        leftArm.TotalSlots.ShouldBe(12);

        rightArm.Location.ShouldBe(PartLocation.RightArm);
        rightArm.MaxArmor.ShouldBe(4);
        rightArm.MaxStructure.ShouldBe(3);
        rightArm.CanBeBlownOff.ShouldBeTrue();
        rightArm.TotalSlots.ShouldBe(12);   
    }
}