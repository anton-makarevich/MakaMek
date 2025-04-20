using Shouldly;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Mechs;

public class LegTests
{
    [Fact]
    public void Leg_ShouldBeInitializedCorrectly()
    {
        var leftLeg = new Leg("LeftLeg", PartLocation.LeftLeg, 8, 4);
        var rightLeg = new Leg("RightLeg", PartLocation.RightLeg, 8, 4);

        leftLeg.Location.ShouldBe(PartLocation.LeftLeg);
        leftLeg.MaxArmor.ShouldBe(8);
        leftLeg.MaxStructure.ShouldBe(4);

        rightLeg.Location.ShouldBe(PartLocation.RightLeg);
        rightLeg.MaxArmor.ShouldBe(8);
        rightLeg.MaxStructure.ShouldBe(4);
    }
}
