using Sanet.MakaMek.Core.Models.Units;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Mechs;

public class MechStructureExtensionsTests
{
    [Theory]
    [InlineData(PartLocation.LeftArm, PartLocation.LeftTorso)]
    [InlineData(PartLocation.RightArm, PartLocation.RightTorso)]
    [InlineData(PartLocation.LeftLeg, PartLocation.LeftTorso)]
    [InlineData(PartLocation.RightLeg, PartLocation.RightTorso)]
    [InlineData(PartLocation.LeftTorso, PartLocation.CenterTorso)]
    [InlineData(PartLocation.RightTorso, PartLocation.CenterTorso)]
    [InlineData(PartLocation.Head, null)]
    [InlineData(PartLocation.CenterTorso, null)]
    public void GetDamageTransferLocation_TransfersToTheAdjacentPart(PartLocation location, PartLocation? expected)
    {
        // Act
        var transferLocation = location.GetDamageTransferLocation();

        // Assert
        transferLocation.ShouldBe(expected);
    }
}
