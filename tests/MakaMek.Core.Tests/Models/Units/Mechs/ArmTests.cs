using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Mechs;

public class ArmTests
{
    [Fact]
    public void Arm_ShouldBeInitializedCorrectly()
    {
        var leftArm = new Arm("Left Arm", PartLocation.LeftArm, 4, 3);
        var rightArm = new Arm("Right Arm", PartLocation.RightArm, 4, 3);

        Assert.Equal(PartLocation.LeftArm, leftArm.Location);
        Assert.Equal(4, leftArm.MaxArmor);
        Assert.Equal(3, leftArm.MaxStructure);

        Assert.Equal(PartLocation.RightArm, rightArm.Location);
        Assert.Equal(4, rightArm.MaxArmor);
        Assert.Equal(3, rightArm.MaxStructure);
    }
}