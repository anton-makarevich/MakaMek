using Sanet.MakaMek.Core.Models.Map;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Mechs;

public class LegTests
{
    [Theory]
    [InlineData(PartLocation.LeftLeg)]
    [InlineData(PartLocation.RightLeg)]
    public void Leg_ShouldBeInitializedCorrectly(PartLocation location)
    {
        var sut = new Leg("Leg", location, 8, 4);

        sut.Location.ShouldBe(location);
        sut.MaxArmor.ShouldBe(8);
        sut.MaxStructure.ShouldBe(4);
        sut.CanBeBlownOff.ShouldBeTrue();
        sut.TotalSlots.ShouldBe(6);
    }
    
    [Fact]
    public void Facing_ShouldMatchUnitPositionFacing()
    {
        var sut = new Leg("LeftLeg", PartLocation.LeftLeg, 8, 4);
        var mech = new Mech("Test", "TST-1A", 4, (List<UnitPart>)[sut]);
        var position = new HexPosition(new HexCoordinates(0, 0), HexDirection.TopRight);
        mech.Deploy(position);
        
        sut.Facing.ShouldBe(position.Facing);
    }
    
    [Fact]
    public void Facing_ShouldNotChangeWhenRotated()
    {
        var sut = new Leg("LeftLeg", PartLocation.LeftLeg, 8, 4);
        var centerTorso = new CenterTorso("Center Torso", 15, 10, 15);
        var mech = new Mech("Test", "TST-1A", 4, (List<UnitPart>)[centerTorso, sut]);
        var position = new HexPosition(new HexCoordinates(0, 0), HexDirection.TopRight);
        mech.Deploy(position);
        
        sut.Facing.ShouldBe(position.Facing);
        
        mech.RotateTorso(HexDirection.Top);
        
        sut.Facing.ShouldBe(HexDirection.TopRight);
    }
    
    [Fact]
    public void Facing_ShouldBeNull_WhenNotDeployed()
    {
        var sut = new Leg("LeftLeg", PartLocation.LeftLeg, 8, 4);
        
        sut.Facing.ShouldBeNull();
    }
}
