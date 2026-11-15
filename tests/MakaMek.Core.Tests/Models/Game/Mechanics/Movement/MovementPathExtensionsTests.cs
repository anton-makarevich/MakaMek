using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
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

    [Fact]
    public void GetDfaLandingPosition_UsesTheLastTranslationAndTheApproachFacing()
    {
        var path = new MovementPath(new List<PathSegment>
        {
            new(new HexPosition(0, 0, HexDirection.Top),
                new HexPosition(1, 0, HexDirection.TopRight), []),
            new(new HexPosition(1, 0, HexDirection.TopRight),
                new HexPosition(2, 0, HexDirection.TopRight), []),
            // A trailing turn in the landing hex is ignored: a jump does not turn in flight.
            new(new HexPosition(2, 0, HexDirection.TopRight),
                new HexPosition(2, 0, HexDirection.Bottom), [])
        }, MovementType.Jump);
        var map = Substitute.For<IBattleMap>();

        var landing = path.GetDfaLandingPosition(map);

        landing.ShouldBe(new HexPosition(2, 0, HexDirection.TopRight));
    }

    [Fact]
    public void GetDfaLandingPosition_ReturnsNull_WhenPathHasNoTranslation()
    {
        var path = new MovementPath(new List<PathSegment>
        {
            new(new HexPosition(0, 0, HexDirection.Top),
                new HexPosition(0, 0, HexDirection.TopRight), [])
        }, MovementType.Jump);

        path.GetDfaLandingPosition(Substitute.For<IBattleMap>()).ShouldBeNull();
    }

    [Fact]
    public void GetDfaLandingPosition_KeepsTheBridgeDeck_WhenItStillExists()
    {
        var path = JumpToBridgePath();
        var map = Substitute.For<IBattleMap>();
        var bridgeHex = new Hex(new HexCoordinates(2, 0));
        bridgeHex.AddTerrain(new BridgeTerrain(1, 100));
        map.GetHex(new HexCoordinates(2, 0)).Returns(bridgeHex);

        var landing = path.GetDfaLandingPosition(map);

        landing.ShouldNotBeNull();
        landing.Surface.ShouldBe(HexSurface.Bridge);
    }

    [Fact]
    public void GetDfaLandingPosition_DropsToTheGround_WhenTheBridgeIsGone()
    {
        var path = JumpToBridgePath();
        var map = Substitute.For<IBattleMap>();
        map.GetHex(new HexCoordinates(2, 0)).Returns(new Hex(new HexCoordinates(2, 0)));

        var landing = path.GetDfaLandingPosition(map);

        landing.ShouldNotBeNull();
        landing.Surface.ShouldBe(HexSurface.Ground);
    }

    private static MovementPath JumpToBridgePath() =>
        new(new List<PathSegment>
        {
            new(new HexPosition(0, 0, HexDirection.Top),
                new HexPosition(1, 0, HexDirection.Top), []),
            new(new HexPosition(1, 0, HexDirection.Top),
                new HexPosition(2, 0, HexDirection.Top, HexSurface.Bridge), [])
        }, MovementType.Jump);

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
