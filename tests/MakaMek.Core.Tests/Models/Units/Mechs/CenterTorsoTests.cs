using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Mechs;

public class CenterTorsoTests
{
    [Fact]
    public void CenterTorso_ShouldBeInitializedCorrectly()
    {
        var sut = new CenterTorso("CenterTorso", 10, 2, 6);

        sut.Location.ShouldBe(PartLocation.CenterTorso);
        sut.MaxArmor.ShouldBe(10);
        sut.MaxRearArmor.ShouldBe(2);
        sut.MaxStructure.ShouldBe(6);
        sut.CanBeBlownOff.ShouldBeFalse();
        sut.TotalSlots.ShouldBe(12);
    }
    
    [Fact]
    public void Facing_ShouldBeNull_WhenNotDeployed()
    {
        var sut = new CenterTorso("CenterTorso", 10, 2, 6);
        sut.Facing.ShouldBeNull();
    }
    
    [Fact]
    public void Facing_ShouldMatchUnitFacing_WhenDeployed()
    {
        var sut = new CenterTorso("CenterTorso", 10, 2, 6);
        var mech = new Mech("Test", "TST-1A", 4, new List<UnitPart> { sut });
        var position = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        mech.Deploy(position);
        
        sut.Facing.ShouldBe(position.Facing);
    }
    
    [Fact]
    public void Facing_ShouldNotChangeWhenRotated()
    {
        var sut = new CenterTorso("CenterTorso", 10, 2, 6);
        var mech = new Mech("Test", "TST-1A", 4, new List<UnitPart> { sut });
        var position = new HexPosition(new HexCoordinates(0, 0), HexDirection.TopRight);
        mech.Deploy(position);
        
        sut.Facing.ShouldBe(position.Facing);
        
        mech.RotateTorso(HexDirection.Top);
        
        sut.Facing.ShouldBe(HexDirection.Top);
    }
}
