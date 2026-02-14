using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Mechs;

public class SideTorsoTests
{
    [Theory]
    [InlineData(PartLocation.LeftTorso)]
    [InlineData(PartLocation.RightTorso)]
    public void SideTorso_ShouldBeInitializedCorrectly(PartLocation location)
    {
        var sut = new SideTorso("Left Torso", location, 8, 2, 5);

        sut.Location.ShouldBe(location);
        sut.MaxArmor.ShouldBe(8);
        sut.MaxRearArmor.ShouldBe(2);
        sut.MaxStructure.ShouldBe(5);
        sut.CurrentRearArmor.ShouldBe(2);
        sut.CanBeBlownOff.ShouldBeFalse();
        sut.TotalSlots.ShouldBe(12);
    }
    
    [Fact]
    public void Facing_ShouldMatchUnitPositionFacing()
    {
        var sut = new SideTorso("LeftTorso", PartLocation.LeftTorso, 8, 2, 5);
        var mech = new Mech("Test", "TST-1A", 4, (List<UnitPart>)[sut]);
        var position = new HexPosition(new HexCoordinates(0, 0), HexDirection.TopRight);
        mech.Deploy(position);
        
        sut.Facing.ShouldBe(position.Facing);
    }

    [Fact]
    public void Facing_ShouldChange_WhenTorsoIsRotated()
    {
        var sut = new SideTorso("LeftTorso", PartLocation.LeftTorso, 8, 2, 5);
        var mech = new Mech("Test", "TST-1A", 4, (List<UnitPart>)[sut]);
        var position = new HexPosition(new HexCoordinates(0, 0), HexDirection.TopRight);
        mech.Deploy(position);
        
        sut.Facing.ShouldBe(position.Facing);
        
        mech.RotateTorso(HexDirection.Top);
        
        sut.Facing.ShouldBe(HexDirection.Top);
    }
}