using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Rules;

/// <summary>
/// The punch and kick location tables, covered for every 2 to 12 result on every column that is
/// specified. The column is chosen by where the attack enters the target's hex relative to the
/// target's facing, which is why a side-arc attack can still roll front/rear.
/// </summary>
public class PhysicalHitLocationTableTests
{
    private readonly TotalWarfareRulesProvider _sut = new();

    [Theory]
    [InlineData(2, PartLocation.CenterTorso)]
    [InlineData(3, PartLocation.RightArm)]
    [InlineData(4, PartLocation.RightArm)]
    [InlineData(5, PartLocation.RightTorso)]
    [InlineData(6, PartLocation.RightTorso)]
    [InlineData(7, PartLocation.CenterTorso)]
    [InlineData(8, PartLocation.LeftTorso)]
    [InlineData(9, PartLocation.LeftTorso)]
    [InlineData(10, PartLocation.LeftArm)]
    [InlineData(11, PartLocation.LeftArm)]
    [InlineData(12, PartLocation.Head)]
    public void GetPunchHitLocation_FrontColumn_CoversEveryResult(int roll, PartLocation expected)
        => _sut.GetPunchHitLocation(roll, HitDirection.Front).ShouldBe(expected);

    [Theory]
    [InlineData(2, PartLocation.CenterTorso)]
    [InlineData(3, PartLocation.RightArm)]
    [InlineData(4, PartLocation.RightArm)]
    [InlineData(5, PartLocation.RightTorso)]
    [InlineData(6, PartLocation.RightTorso)]
    [InlineData(7, PartLocation.CenterTorso)]
    [InlineData(8, PartLocation.LeftTorso)]
    [InlineData(9, PartLocation.LeftTorso)]
    [InlineData(10, PartLocation.LeftArm)]
    [InlineData(11, PartLocation.LeftArm)]
    [InlineData(12, PartLocation.Head)]
    public void GetPunchHitLocation_RearColumn_ReadsTheSameAsFront(int roll, PartLocation expected)
        => _sut.GetPunchHitLocation(roll, HitDirection.Rear).ShouldBe(expected);

    /// <summary>
    /// A roll of 2 is the center-torso critical result rather than an ordinary center-torso hit.
    /// The distinction is the resolver's to apply; the table only has to land on center torso.
    /// </summary>
    [Fact]
    public void GetPunchHitLocation_TwoIsTheCenterTorsoCriticalResult()
        => _sut.GetPunchHitLocation(2, HitDirection.Front).ShouldBe(PartLocation.CenterTorso);

    /// <summary>
    /// Deliberately not implemented. #1501 specifies only the front/rear punch column and
    /// docs/rules/physical-attacks.md names the Punch Location Table without giving its rows, so
    /// the side columns are a question for the maintainer rather than something to invent. Replace
    /// this test with the real columns once they are specified.
    /// </summary>
    [Theory]
    [InlineData(HitDirection.Left)]
    [InlineData(HitDirection.Right)]
    public void GetPunchHitLocation_SideColumns_AreNotSpecifiedYet(HitDirection direction)
        => Should.Throw<NotSupportedException>(() => _sut.GetPunchHitLocation(7, direction));

    [Theory]
    [InlineData(2, PartLocation.LeftLeg)]
    [InlineData(3, PartLocation.LeftLeg)]
    [InlineData(4, PartLocation.LeftLeg)]
    [InlineData(5, PartLocation.LeftLeg)]
    [InlineData(6, PartLocation.LeftLeg)]
    [InlineData(7, PartLocation.LeftLeg)]
    [InlineData(8, PartLocation.LeftLeg)]
    [InlineData(9, PartLocation.RightLeg)]
    [InlineData(10, PartLocation.RightLeg)]
    [InlineData(11, PartLocation.RightLeg)]
    [InlineData(12, PartLocation.RightLeg)]
    public void GetKickHitLocation_LeftColumn_NearLegOnTwoToEight(int roll, PartLocation expected)
        => _sut.GetKickHitLocation(roll, HitDirection.Left).ShouldBe(expected);

    [Theory]
    [InlineData(2, PartLocation.RightLeg)]
    [InlineData(3, PartLocation.RightLeg)]
    [InlineData(4, PartLocation.RightLeg)]
    [InlineData(5, PartLocation.RightLeg)]
    [InlineData(6, PartLocation.RightLeg)]
    [InlineData(7, PartLocation.RightLeg)]
    [InlineData(8, PartLocation.RightLeg)]
    [InlineData(9, PartLocation.LeftLeg)]
    [InlineData(10, PartLocation.LeftLeg)]
    [InlineData(11, PartLocation.LeftLeg)]
    [InlineData(12, PartLocation.LeftLeg)]
    public void GetKickHitLocation_RightColumn_NearLegOnTwoToEight(int roll, PartLocation expected)
        => _sut.GetKickHitLocation(roll, HitDirection.Right).ShouldBe(expected);

    [Theory]
    [InlineData(2, PartLocation.RightLeg)]
    [InlineData(3, PartLocation.RightLeg)]
    [InlineData(4, PartLocation.RightLeg)]
    [InlineData(5, PartLocation.RightLeg)]
    [InlineData(6, PartLocation.RightLeg)]
    [InlineData(7, PartLocation.RightLeg)]
    [InlineData(8, PartLocation.RightLeg)]
    [InlineData(9, PartLocation.LeftLeg)]
    [InlineData(10, PartLocation.LeftLeg)]
    [InlineData(11, PartLocation.LeftLeg)]
    [InlineData(12, PartLocation.LeftLeg)]
    public void GetKickHitLocation_FrontColumn_RightLegOnTwoToEight(int roll, PartLocation expected)
        => _sut.GetKickHitLocation(roll, HitDirection.Front).ShouldBe(expected);

    [Theory]
    [InlineData(2, PartLocation.RightLeg)]
    [InlineData(8, PartLocation.RightLeg)]
    [InlineData(9, PartLocation.LeftLeg)]
    [InlineData(12, PartLocation.LeftLeg)]
    public void GetKickHitLocation_RearColumn_ReadsTheSameAsFront(int roll, PartLocation expected)
        => _sut.GetKickHitLocation(roll, HitDirection.Rear).ShouldBe(expected);

    [Theory]
    [InlineData(1)]
    [InlineData(13)]
    [InlineData(0)]
    public void GetPunchHitLocation_RejectsImpossibleRolls(int roll)
        => Should.Throw<ArgumentOutOfRangeException>(
            () => _sut.GetPunchHitLocation(roll, HitDirection.Front));

    [Theory]
    [InlineData(1)]
    [InlineData(13)]
    [InlineData(0)]
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
        for (var roll = 2; roll <= 12; roll++)
        {
            _sut.GetPunchHitLocation(roll, HitDirection.Front)
                .ShouldBe(_sut.GetPunchHitLocation(roll, HitDirection.Front));
            _sut.GetKickHitLocation(roll, HitDirection.Left)
                .ShouldBe(_sut.GetKickHitLocation(roll, HitDirection.Left));
        }
    }
}
