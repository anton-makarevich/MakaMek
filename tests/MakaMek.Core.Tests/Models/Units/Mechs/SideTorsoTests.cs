using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Mechs;

public class SideTorsoTests
{
    [Fact]
    public void SideTorso_ShouldBeInitializedCorrectly()
    {
        var leftTorso = new SideTorso("Left Torso", PartLocation.LeftTorso, 8, 2, 5);
        var rightTorso = new SideTorso("Right Torso", PartLocation.RightTorso, 8, 2, 5);

        leftTorso.Location.ShouldBe(PartLocation.LeftTorso);
        leftTorso.MaxArmor.ShouldBe(8);
        leftTorso.MaxRearArmor.ShouldBe(2);
        leftTorso.MaxStructure.ShouldBe(5);
        leftTorso.CurrentRearArmor.ShouldBe(2);
        leftTorso.CanBeBlownOff.ShouldBeFalse();
        leftTorso.TotalSlots.ShouldBe(12);

        rightTorso.Location.ShouldBe(PartLocation.RightTorso);
        rightTorso.MaxArmor.ShouldBe(8);
        rightTorso.MaxRearArmor.ShouldBe(2);
        rightTorso.MaxStructure.ShouldBe(5);
        rightTorso.CurrentRearArmor.ShouldBe(2);
        rightTorso.CanBeBlownOff.ShouldBeFalse();
        rightTorso.TotalSlots.ShouldBe(12);
    }
}