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
}
