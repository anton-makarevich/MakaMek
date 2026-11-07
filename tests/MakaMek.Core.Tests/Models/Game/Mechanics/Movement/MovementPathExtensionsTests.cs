using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Movement;

public class MovementPathExtensionsTests
{
    [Fact]
    public void GetStopPosition_ReturnsNull_WhenDeclarationIsNull()
    {
        TwoHexPath().GetStopPosition(null).ShouldBeNull();
    }

    [Fact]
    public void GetStopPosition_ReturnsNull_ForNonMovementAttack()
    {
        TwoHexPath().GetStopPosition(Declaration(PhysicalAttackType.Punch)).ShouldBeNull();
    }

    [Fact]
    public void GetStopPosition_ReturnsApproachHex_ForChargeWithTrailingTurns()
    {
        var path = new MovementPath(new List<PathSegment>
        {
            new(new HexPosition(0, 0, HexDirection.Top),
                new HexPosition(1, 0, HexDirection.Top), []),
            new(new HexPosition(1, 0, HexDirection.Top),
                new HexPosition(2, 0, HexDirection.Top), []),
            new(new HexPosition(2, 0, HexDirection.Top),
                new HexPosition(2, 0, HexDirection.TopRight), [])
        }, MovementType.Walk);

        var stop = path.GetStopPosition(Declaration(PhysicalAttackType.Charge));

        stop.ShouldBe(new HexPosition(1, 0, HexDirection.Top));
    }

    [Fact]
    public void GetStopPosition_ReturnsLastJumpPathHex_ForDfa()
    {
        var path = new MovementPath(new List<PathSegment>
        {
            new(new HexPosition(0, 0, HexDirection.Top),
                new HexPosition(1, 0, HexDirection.TopRight), []),
            new(new HexPosition(1, 0, HexDirection.TopRight),
                new HexPosition(2, 0, HexDirection.TopRight), [])
        }, MovementType.Jump);

        var stop = path.GetStopPosition(Declaration(PhysicalAttackType.DFA));

        stop.ShouldBe(new HexPosition(1, 0, HexDirection.TopRight));
    }

    [Fact]
    public void GetStopPosition_ReturnsNull_WhenPathHasNoTranslation()
    {
        var path = new MovementPath(new List<PathSegment>
        {
            new(new HexPosition(0, 0, HexDirection.Top),
                new HexPosition(0, 0, HexDirection.TopRight), [])
        }, MovementType.Walk);

        path.GetStopPosition(Declaration(PhysicalAttackType.Charge)).ShouldBeNull();
    }

    private static MovementPath TwoHexPath() =>
        new(new List<PathSegment>
        {
            new(new HexPosition(0, 0, HexDirection.Top),
                new HexPosition(1, 0, HexDirection.Top), []),
            new(new HexPosition(1, 0, HexDirection.Top),
                new HexPosition(2, 0, HexDirection.Top), [])
        }, MovementType.Walk);

    private static PhysicalAttackDeclarationData Declaration(PhysicalAttackType attackType) =>
        new()
        {
            AttackType = attackType,
            AttackerLimbs = [],
            TargetId = Guid.NewGuid()
        };
}
