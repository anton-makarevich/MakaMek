using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Rules;

/// <summary>
/// The punch and kick location tables, covered for every 1 to 6 result on every column that is
/// specified. The column is chosen by where the attack enters the target's hex relative to the
/// target's facing, which is why a side-arc attack can still roll front/rear.
/// </summary>
public class PhysicalHitLocationTableTests
{
    private readonly TotalWarfareRulesProvider _sut = new();

    [Theory]
    [InlineData(1, PartLocation.LeftArm)]
    [InlineData(2, PartLocation.LeftTorso)]
    [InlineData(3, PartLocation.CenterTorso)]
    [InlineData(4, PartLocation.RightTorso)]
    [InlineData(5, PartLocation.RightArm)]
    [InlineData(6, PartLocation.Head)]
    public void GetPunchHitLocation_FrontColumn_CoversEveryResult(int roll, PartLocation expected)
        => _sut.GetPunchHitLocation(roll, HitDirection.Front).ShouldBe(expected);

    [Theory]
    [InlineData(1, PartLocation.LeftArm)]
    [InlineData(2, PartLocation.LeftTorso)]
    [InlineData(3, PartLocation.CenterTorso)]
    [InlineData(4, PartLocation.RightTorso)]
    [InlineData(5, PartLocation.RightArm)]
    [InlineData(6, PartLocation.Head)]
    public void GetPunchHitLocation_RearColumn_ReadsTheSameAsFront(int roll, PartLocation expected)
        => _sut.GetPunchHitLocation(roll, HitDirection.Rear).ShouldBe(expected);

    [Theory]
    [InlineData(1, PartLocation.LeftTorso)]
    [InlineData(2, PartLocation.LeftTorso)]
    [InlineData(3, PartLocation.CenterTorso)]
    [InlineData(4, PartLocation.LeftArm)]
    [InlineData(5, PartLocation.LeftArm)]
    [InlineData(6, PartLocation.Head)]
    public void GetPunchHitLocation_LeftColumn_CoversEveryResult(int roll, PartLocation expected)
        => _sut.GetPunchHitLocation(roll, HitDirection.Left).ShouldBe(expected);

    [Theory]
    [InlineData(1, PartLocation.RightTorso)]
    [InlineData(2, PartLocation.RightTorso)]
    [InlineData(3, PartLocation.CenterTorso)]
    [InlineData(4, PartLocation.RightArm)]
    [InlineData(5, PartLocation.RightArm)]
    [InlineData(6, PartLocation.Head)]
    public void GetPunchHitLocation_RightColumn_CoversEveryResult(int roll, PartLocation expected)
        => _sut.GetPunchHitLocation(roll, HitDirection.Right).ShouldBe(expected);

    [Theory]
    [InlineData(1, PartLocation.LeftLeg)]
    [InlineData(2, PartLocation.LeftLeg)]
    [InlineData(3, PartLocation.LeftLeg)]
    [InlineData(4, PartLocation.LeftLeg)]
    [InlineData(5, PartLocation.LeftLeg)]
    [InlineData(6, PartLocation.LeftLeg)]
    public void GetKickHitLocation_LeftColumn_AlwaysHitsTheLeftLeg(int roll, PartLocation expected)
        => _sut.GetKickHitLocation(roll, HitDirection.Left).ShouldBe(expected);

    [Theory]
    [InlineData(1, PartLocation.RightLeg)]
    [InlineData(2, PartLocation.RightLeg)]
    [InlineData(3, PartLocation.RightLeg)]
    [InlineData(4, PartLocation.RightLeg)]
    [InlineData(5, PartLocation.RightLeg)]
    [InlineData(6, PartLocation.RightLeg)]
    public void GetKickHitLocation_RightColumn_AlwaysHitsTheRightLeg(int roll, PartLocation expected)
        => _sut.GetKickHitLocation(roll, HitDirection.Right).ShouldBe(expected);

    [Theory]
    [InlineData(1, PartLocation.RightLeg)]
    [InlineData(2, PartLocation.RightLeg)]
    [InlineData(3, PartLocation.RightLeg)]
    [InlineData(4, PartLocation.LeftLeg)]
    [InlineData(5, PartLocation.LeftLeg)]
    [InlineData(6, PartLocation.LeftLeg)]
    public void GetKickHitLocation_FrontColumn_RightLegOnOneToThree(int roll, PartLocation expected)
        => _sut.GetKickHitLocation(roll, HitDirection.Front).ShouldBe(expected);

    [Theory]
    [InlineData(1, PartLocation.RightLeg)]
    [InlineData(2, PartLocation.RightLeg)]
    [InlineData(3, PartLocation.RightLeg)]
    [InlineData(4, PartLocation.LeftLeg)]
    [InlineData(5, PartLocation.LeftLeg)]
    [InlineData(6, PartLocation.LeftLeg)]
    public void GetKickHitLocation_RearColumn_ReadsTheSameAsFront(int roll, PartLocation expected)
        => _sut.GetKickHitLocation(roll, HitDirection.Rear).ShouldBe(expected);

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-1)]
    public void GetPunchHitLocation_RejectsImpossibleRolls(int roll)
        => Should.Throw<ArgumentOutOfRangeException>(
            () => _sut.GetPunchHitLocation(roll, HitDirection.Front));

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-1)]
    public void GetKickHitLocation_RejectsImpossibleRolls(int roll)
        => Should.Throw<ArgumentOutOfRangeException>(
            () => _sut.GetKickHitLocation(roll, HitDirection.Front));

    /// <summary>
    /// Both tables are pure lookups, so the same inputs have to keep giving the same answer. A
    /// resolver that rolls twice must not be able to change the location it already reported.
    /// </summary>
    [Fact]
    public void BothTables_AreStableAcrossCalls()
    {
        for (var roll = 1; roll <= 6; roll++)
        {
            _sut.GetPunchHitLocation(roll, HitDirection.Front)
                .ShouldBe(_sut.GetPunchHitLocation(roll, HitDirection.Front));
            _sut.GetKickHitLocation(roll, HitDirection.Left)
                .ShouldBe(_sut.GetKickHitLocation(roll, HitDirection.Left));
        }
    }
}
