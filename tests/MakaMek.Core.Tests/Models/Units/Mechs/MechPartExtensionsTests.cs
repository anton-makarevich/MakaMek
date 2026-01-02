using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Mechs;

public class MechPartExtensionsTests
{
    [Fact]
    public void GetAvailableTorsoRotationOptions_WhenMechCanRotateTorso_ShouldReturnCorrectOptions()
    {
        var part = new Arm("Test", PartLocation.LeftArm, 4, 3);
        var mech = new Mech("Test", "TST-1A", 50, [part], possibleTorsoRotation: 1);
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));

        var options = part.GetAvailableTorsoRotationOptions();

        options.Count.ShouldBe(1);
        options[0].Type.ShouldBe(WeaponConfigurationType.TorsoRotation);
        options[0].AvailableDirections.ShouldBe([HexDirection.TopRight, HexDirection.TopLeft]);
    }
    
    [Fact]
    public void GetAvailableTorsoRotationOptions_WhenMechCannotRotateTorso_ShouldReturnEmptyOptions()
    {
        var part = new Arm("Test", PartLocation.LeftArm, 4, 3);
        var mech = new Mech("Test", "TST-1A", 50, [part], possibleTorsoRotation: 0);
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));

        var options = part.GetAvailableTorsoRotationOptions();

        options.ShouldBeEmpty();
    }
    
    [Fact]
    public void GetAvailableTorsoRotationOptions_WhenMechIsNotDeployed_ShouldReturnEmptyOptions()
    {
        var part = new Arm("Test", PartLocation.LeftArm, 4, 3);
        var mech = new Mech("Test", "TST-1A", 50, [part], possibleTorsoRotation: 1);

        var options = part.GetAvailableTorsoRotationOptions();

        mech.IsDeployed.ShouldBeFalse();
        options.ShouldBeEmpty();
    }
    
    [Fact]
    public void GetAvailableTorsoRotationOptions_WhenUnitIsNotMech_ShouldReturnEmptyOptions()
    {
        var part = new Arm("Test", PartLocation.LeftArm, 4, 3);
        var unit = new UnitTests.TestUnit("Test", "TST-1A", 50, [part]);
        unit.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));

        var options = part.GetAvailableTorsoRotationOptions();

        options.ShouldBeEmpty();
    }

    [Fact]
    public void GetAvailableTorsoRotationOptions_ShouldReturnCorrectOptions_WhenForwardPositionIsOverridden()
    {
        var part = new Arm("Test", PartLocation.LeftArm, 4, 3);
        var mech = new Mech("Test", "TST-1A", 50, [part], possibleTorsoRotation: 1);
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));
        var forwardPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        
        var options = part.GetAvailableTorsoRotationOptions(forwardPosition);

        options.Count.ShouldBe(1);
        options[0].Type.ShouldBe(WeaponConfigurationType.TorsoRotation);
        options[0].AvailableDirections.ShouldBe([HexDirection.BottomRight, HexDirection.BottomLeft]);
    }
}