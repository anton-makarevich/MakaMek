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
        mech.Deploy(position, null);
        
        sut.Facing.ShouldBe(position.Facing);
    }

    [Fact]
    public void Facing_ShouldChange_WhenTorsoIsRotated()
    {
        var sut = new SideTorso("LeftTorso", PartLocation.LeftTorso, 8, 2, 5);
        var mech = new Mech("Test", "TST-1A", 4, (List<UnitPart>)[sut]);
        var position = new HexPosition(new HexCoordinates(0, 0), HexDirection.TopRight);
        mech.Deploy(position, null);
        
        sut.Facing.ShouldBe(position.Facing);
        
        mech.RotateTorso(HexDirection.Top);
        
        sut.Facing.ShouldBe(HexDirection.Top);
    }

    [Theory]
    [InlineData(PartLocation.LeftTorso, PartLocation.LeftArm)]
    [InlineData(PartLocation.RightTorso, PartLocation.RightArm)]
    public void ApplyBreach_ShouldFloodConnectedArm(PartLocation torsoLocation, PartLocation armLocation)
    {
        var torso = new SideTorso("SideTorso", torsoLocation, 8, 2, 5);
        var arm = new Arm("Arm", armLocation, 10, 5);
        _ = new Mech("Test", "TST-1A", 50, (List<UnitPart>)[torso, arm]);

        torso.ApplyBreach();

        foreach (var component in arm.Components)
        {
            component.IsFlooded.ShouldBeTrue();
        }
    }

    [Fact]
    public void ApplyBreach_ShouldNotThrow_WhenArmNotFound()
    {
        var torso = new SideTorso("SideTorso", PartLocation.LeftTorso, 8, 2, 5);
        // Create mech with only the torso, no arm

        torso.ApplyBreach();
    }

    [Fact]
    public void ApplyBreach_ShouldFloodOwnComponents()
    {
        var sut = new SideTorso("SideTorso", PartLocation.LeftTorso, 8, 2, 5);

        sut.ApplyBreach();

        sut.IsBreached.ShouldBeTrue();
        foreach (var component in sut.Components)
        {
            component.IsFlooded.ShouldBeTrue();
        }
    }

    [Fact]
    public void Level_ShouldReturn2_WhenNotMounted()
    {
        var sut = new SideTorso("LeftTorso", PartLocation.LeftTorso, 8, 2, 5);
        sut.Level.ShouldBe(2);
    }
    
    [Fact]
    public void Level_ShouldReturnUnitHeight_WhenMounted()
    {
        var sut = new SideTorso("LeftTorso", PartLocation.LeftTorso, 8, 2, 5);
        var mech = new Mech("Test", "TST-1A", 4, (List<UnitPart>)[sut]);
        
        sut.Level.ShouldBe(2);
        
        mech.SetProne();
        
        sut.Level.ShouldBe(1);
    }
}