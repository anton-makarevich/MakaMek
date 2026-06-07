using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Tests.Models.Game.Phases;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Movement.Interrupters;

public class JumpDamageInterruptHandlerTests : GamePhaseTestsBase
{
    private readonly JumpDamageInterruptHandler _sut = new();
    private Guid _unitId;

    protected override void SetupSut()
    {
        var playerId = Guid.NewGuid();
        Game.HandleCommand(CreateJoinCommand(playerId, "Player 1"));
        _unitId = Game.Players[0].Units[0].Id;
        SetMap();
    }

    private MovementInterruptContext CreateContext(MoveUnitCommand moveCommand, int segmentIndex, bool isLandingCheck = false)
    {
        var unit = Game.Players[0].Units.Single(u => u.Id == moveCommand.UnitId);
        return new MovementInterruptContext
        {
            MoveCommand = moveCommand,
            SegmentIndex = segmentIndex,
            Unit = unit,
            Game = Game,
            IsLandingCheck = isLandingCheck
        };
    }

    private static MoveUnitCommand CreateMoveCommand(Guid unitId, MovementType movementType, params PathSegment[] segments) =>
        new()
        {
            MovementType = movementType,
            GameOriginId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            UnitId = unitId,
            MovementPath = segments.Select(s => s.ToData()).ToList()
        };

    [Fact]
    public void JumpDamageInterruptHandler_Check_WhenWalk_ReturnsNull()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Walk,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0)).ShouldBeNull();
    }

    [Fact]
    public void JumpDamageInterruptHandler_Check_WhenJumpNotLanding_ReturnsNull()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Jump,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0)).ShouldBeNull();
    }

    [Fact]
    public void JumpDamageInterruptHandler_Check_WhenJumpWithoutDamage_ReturnsNull()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Jump,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0, isLandingCheck: true)).ShouldBeNull();
    }

    [Fact]
    public void JumpDamageInterruptHandler_Check_WhenJumpPsrPasses_ReturnsPsrOnly()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var gyro = mech.GetAllComponents<Gyro>().First();
        gyro.Hit();

        var successContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = false,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new PilotingSkillRollContext(PilotingSkillRollType.JumpWithDamage),
                DiceResults = [10, 10],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            }
        };
        MockFallProcessor.ProcessMovementAttempt(mech, Arg.Any<PilotingSkillRollContext>(), Game, MovementType.Jump)
            .Returns(successContext);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Jump,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0, isLandingCheck: true));

        result.ShouldNotBeNull();
        result.ShouldStop.ShouldBeFalse();
        result.GameActions.ShouldHaveSingleItem();
        result.GameActions[0].ShouldBeOfType<PublishCommandAction>();
    }

    [Fact]
    public void JumpDamageInterruptHandler_Check_WhenJumpPsrFails_ReturnsStopWithFall()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var gyro = mech.GetAllComponents<Gyro>().First();
        gyro.Hit();

        var fallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new PilotingSkillRollContext(PilotingSkillRollType.JumpWithDamage),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3),
                HitDirection.Front)
        };
        MockFallProcessor.ProcessMovementAttempt(mech, Arg.Any<PilotingSkillRollContext>(), Game, MovementType.Jump)
            .Returns(fallContext);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Jump,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0, isLandingCheck: true));

        result.ShouldNotBeNull();
        result.ShouldStop.ShouldBeTrue();
        result.GameActions.ShouldHaveSingleItem();
        result.GameActions[0].ShouldBeOfType<ApplyFallAction>();
    }
}
