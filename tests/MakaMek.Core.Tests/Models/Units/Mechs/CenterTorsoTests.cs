using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Mechs;

public class CenterTorsoTests
{
    [Fact]
    public void CenterTorso_ShouldBeInitializedCorrectly()
    {
        var centerTorso = new CenterTorso("CenterTorso", 10, 2, 6);

        centerTorso.Location.ShouldBe(PartLocation.CenterTorso);
        centerTorso.MaxArmor.ShouldBe(10);
        centerTorso.MaxRearArmor.ShouldBe(2);
        centerTorso.MaxStructure.ShouldBe(6);
        centerTorso.CanBeBlownOff.ShouldBeFalse();
        centerTorso.TotalSlots.ShouldBe(12);
    }
}
