using Sanet.MakaMek.Core.Models.Units;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units;

public class PartLocationTests
{
    [Theory]
    [InlineData(PartLocation.LeftLeg, true)]
    [InlineData(PartLocation.RightLeg, true)]
    [InlineData(PartLocation.Head, false)]
    [InlineData(PartLocation.CenterTorso, false)]
    [InlineData(PartLocation.LeftTorso, false)]
    [InlineData(PartLocation.RightTorso, false)]
    [InlineData(PartLocation.LeftArm, false)]
    [InlineData(PartLocation.RightArm, false)]
    public void IsLeg_ChecksIfLocationIsLeg_ReturnsCorrectResult(PartLocation location, bool expected)
    {
        // Act
        var result = location.IsLeg();

        // Assert
        result.ShouldBe(expected);
    }
    
    [Theory]
    [InlineData(PartLocation.LeftArm, true)]
    [InlineData(PartLocation.RightArm, true)]
    [InlineData(PartLocation.Head, false)]
    [InlineData(PartLocation.CenterTorso, false)]
    [InlineData(PartLocation.LeftTorso, false)]
    [InlineData(PartLocation.RightTorso, false)]
    [InlineData(PartLocation.LeftLeg, false)]
    [InlineData(PartLocation.RightLeg, false)]
    public void IsArm_ChecksIfLocationIsArm_ReturnsCorrectResult(PartLocation location, bool expected)
    {
        // Act
        var result = location.IsArm();

        // Assert
        result.ShouldBe(expected);
    }
}
