using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Mechs;

public class SideTorsoTests
{
    [Fact]
    public void SideTorso_ShouldBeInitializedCorrectly()
    {
        var leftTorso = new SideTorso("Left Torso", PartLocation.LeftTorso, 8, 2, 5);
        var rightTorso = new SideTorso("Right Torso", PartLocation.RightTorso, 8, 2, 5);

        Assert.Equal(PartLocation.LeftTorso, leftTorso.Location);
        Assert.Equal(8, leftTorso.MaxArmor);
        Assert.Equal(2, leftTorso.MaxRearArmor);
        Assert.Equal(5, leftTorso.MaxStructure);

        Assert.Equal(PartLocation.RightTorso, rightTorso.Location);
        Assert.Equal(8, rightTorso.MaxArmor);
        Assert.Equal(2, rightTorso.MaxRearArmor);
        Assert.Equal(5, rightTorso.MaxStructure);
    }
}