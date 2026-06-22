using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Tests.Models.Game.Phases;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Movement.Interrupters;

public class SkidInterruptHandlerTests : GamePhaseTestsBase
{
    private readonly SkidInterruptHandler _sut = new();
    private Guid _unitId;

    protected override void SetupSut()
    {
        var playerId = Guid.NewGuid();
        Game.HandleCommand(CreateJoinCommand(playerId, "Player 1"));
        _unitId = Game.Players[0].Units[0].Id;
        SetMap();
    }

    private MovementInterruptContext CreateContext(MoveUnitCommand moveCommand, int segmentIndex)
    {
        var unit = Game.Players[0].Units.Single(u => u.Id == moveCommand.UnitId);
        return new MovementInterruptContext
        {
            MoveCommand = moveCommand,
            SegmentIndex = segmentIndex,
            Unit = unit,
            Game = Game
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
    public void SkidInterruptHandler_Check_WhenWalk_ReturnsNull()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new RoadTerrain());

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Walk,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []),
            new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Bottom), []),
            new PathSegment(new HexPosition(2, 2, HexDirection.Bottom), new HexPosition(3, 2, HexDirection.Bottom), []));

        _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 1)).ShouldBeNull();
    }

    [Fact]
    public void SkidInterruptHandler_Check_WhenSkidPasses_ReturnsPsrOnly()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        Game.BattleMap!.GetHex(new HexCoordinates(3, 2))!.AddTerrain(new RoadTerrain());

        var successContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = false,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(1, 3),
                DiceResults = [10, 10],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            }
        };
        MockFallProcessor.ProcessMovementAttempt(
            mech,
            Arg.Is<SkidCheckRollContext>(ctx => ctx.SkidDistance == 1 && ctx.HexesMoved == 2),
            Game,
            MovementType.Run)
            .Returns(successContext);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Run,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []),
            new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), []),
            new PathSegment(new HexPosition(3, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Bottom), []),
            new PathSegment(new HexPosition(3, 2, HexDirection.Bottom), new HexPosition(4, 2, HexDirection.Bottom), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 2));

        result.ShouldNotBeNull();
        result.ShouldStop.ShouldBeFalse();
        result.GameActions.ShouldHaveSingleItem();
        result.GameActions[0].ShouldBeOfType<PublishCommandAction>();
    }

    [Fact]
    public void SkidInterruptHandler_Check_WhenSkidFails_ReturnsStopWithFall()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        Game.BattleMap!.GetHex(new HexCoordinates(3, 2))!.AddTerrain(new RoadTerrain());

        var fallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(1, 3),
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
        MockFallProcessor.ProcessMovementAttempt(
            mech,
            Arg.Is<SkidCheckRollContext>(ctx => ctx.SkidDistance == 1 && ctx.HexesMoved == 2),
            Game,
            MovementType.Run)
            .Returns(fallContext);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Run,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []),
            new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), []),
            new PathSegment(new HexPosition(3, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Bottom), []),
            new PathSegment(new HexPosition(3, 2, HexDirection.Bottom), new HexPosition(4, 2, HexDirection.Bottom), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 2));

        result.ShouldNotBeNull();
        result.ShouldStop.ShouldBeTrue();
        result.GameActions.ShouldContain(a => a is MoveUnitAction);
        result.GameActions.ShouldContain(a => a is ApplyFallAction);
    }

    [Fact]
    public void SkidInterruptHandler_Check_WhenSkidFailsWithSkidDamage_ReturnsStopWithFallAndSkidAction()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        Game.BattleMap!.GetHex(new HexCoordinates(3, 2))!.AddTerrain(new RoadTerrain());

        var fallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(1, 3),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3),
                HitDirection.Front),
            SkidDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 3),
                new DiceResult(2),
                HitDirection.Front),
            SkidDistance = 1
        };
        MockFallProcessor.ProcessMovementAttempt(
            mech,
            Arg.Is<SkidCheckRollContext>(ctx => ctx.SkidDistance == 1 && ctx.HexesMoved == 2),
            Game,
            MovementType.Run)
            .Returns(fallContext);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Run,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []),
            new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), []),
            new PathSegment(new HexPosition(3, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Bottom), []),
            new PathSegment(new HexPosition(3, 2, HexDirection.Bottom), new HexPosition(4, 2, HexDirection.Bottom), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 2));

        result.ShouldNotBeNull();
        result.ShouldStop.ShouldBeTrue();
        result.GameActions.ShouldContain(a => a is MoveUnitAction);
        result.GameActions.ShouldContain(a => a is ApplyFallAction);
        result.GameActions.ShouldContain(a => a is ApplySkidAction);
    }

    [Fact]
    public void SkidInterruptHandler_Check_WhenRunTurnOnNonRoad_ReturnsNull()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        // No road terrain added - hex (2,2) is clear by default

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Run,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []),
            new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Bottom), []));

        _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 1)).ShouldBeNull();
    }

    [Fact]
    public void SkidInterruptHandler_Check_WhenRunTurnOnRoadLastHex_ReturnsNull()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new RoadTerrain());

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Run,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []),
            new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Bottom), []));

        _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 1)).ShouldBeNull();
    }

    [Fact]
    public void SkidInterruptHandler_Check_WhenWaterDepth1OnGround_StopsSkidAtWater()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 3, HexDirection.Top), null);
        Game.BattleMap!.GetHex(new HexCoordinates(4, 3))!.AddTerrain(new RoadTerrain());
        Game.BattleMap!.GetHex(new HexCoordinates(4, 2))!.AddTerrain(new WaterTerrain(-1));

        var successContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = false,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(1, 3),
                DiceResults = [10, 10],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            }
        };
        MockFallProcessor.ProcessMovementAttempt(
            mech,
            Arg.Is<SkidCheckRollContext>(ctx => ctx.SkidDistance == 1),
            Game,
            MovementType.Run)
            .Returns(successContext);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Run,
            new PathSegment(new HexPosition(1, 3, HexDirection.Top), new HexPosition(2, 3, HexDirection.Top), []),
            new PathSegment(new HexPosition(2, 3, HexDirection.Top), new HexPosition(3, 3, HexDirection.Top), []),
            new PathSegment(new HexPosition(3, 3, HexDirection.Top), new HexPosition(4, 3, HexDirection.Top), []),
            new PathSegment(new HexPosition(4, 3, HexDirection.Top), new HexPosition(4, 3, HexDirection.Bottom), []),
            new PathSegment(new HexPosition(4, 3, HexDirection.Bottom), new HexPosition(5, 3, HexDirection.Bottom), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 3));

        result.ShouldNotBeNull();
        result.ShouldStop.ShouldBeFalse();
        result.GameActions.ShouldHaveSingleItem();
        result.GameActions[0].ShouldBeOfType<PublishCommandAction>();
    }

    [Fact]
    public void SkidInterruptHandler_Check_WhenWaterDepth1OnBridge_DoesNotStopSkid()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 3, HexDirection.Top), null);
        Game.BattleMap!.GetHex(new HexCoordinates(4, 3))!.AddTerrain(new BridgeTerrain(2, 40));
        Game.BattleMap!.GetHex(new HexCoordinates(4, 2))!.AddTerrain(new BridgeTerrain(2, 40));
        Game.BattleMap!.GetHex(new HexCoordinates(4, 2))!.AddTerrain(new WaterTerrain(-1));

        var successContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = false,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(2, 3),
                DiceResults = [10, 10],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            }
        };
        MockFallProcessor.ProcessMovementAttempt(
            mech,
            Arg.Is<SkidCheckRollContext>(ctx => ctx.SkidDistance == 2),
            Game,
            MovementType.Run)
            .Returns(successContext);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Run,
            new PathSegment(new HexPosition(1, 3, HexDirection.Top), new HexPosition(2, 3, HexDirection.Top), []),
            new PathSegment(new HexPosition(2, 3, HexDirection.Top), new HexPosition(3, 3, HexDirection.Top), []),
            new PathSegment(new HexPosition(3, 3, HexDirection.Top), new HexPosition(4, 3, HexDirection.Top, HexSurface.Bridge), []),
            new PathSegment(new HexPosition(4, 3, HexDirection.Top, HexSurface.Bridge), new HexPosition(4, 3, HexDirection.Bottom, HexSurface.Bridge), []),
            new PathSegment(new HexPosition(4, 3, HexDirection.Bottom, HexSurface.Bridge), new HexPosition(5, 3, HexDirection.Bottom), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 3));

        result.ShouldNotBeNull();
        result.ShouldStop.ShouldBeFalse();
        result.GameActions.ShouldHaveSingleItem();
        result.GameActions[0].ShouldBeOfType<PublishCommandAction>();
    }

    [Fact]
    public void SkidInterruptHandler_Check_WhenWaterLevel0OnGround_DoesNotStopSkid()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 3, HexDirection.Top), null);
        Game.BattleMap!.GetHex(new HexCoordinates(4, 3))!.AddTerrain(new RoadTerrain());
        Game.BattleMap!.GetHex(new HexCoordinates(4, 2))!.AddTerrain(new WaterTerrain(0));

        var successContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = false,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(2, 3),
                DiceResults = [10, 10],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            }
        };
        MockFallProcessor.ProcessMovementAttempt(
            mech,
            Arg.Is<SkidCheckRollContext>(ctx => ctx.SkidDistance == 2),
            Game,
            MovementType.Run)
            .Returns(successContext);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Run,
            new PathSegment(new HexPosition(1, 3, HexDirection.Top), new HexPosition(2, 3, HexDirection.Top), []),
            new PathSegment(new HexPosition(2, 3, HexDirection.Top), new HexPosition(3, 3, HexDirection.Top), []),
            new PathSegment(new HexPosition(3, 3, HexDirection.Top), new HexPosition(4, 3, HexDirection.Top), []),
            new PathSegment(new HexPosition(4, 3, HexDirection.Top), new HexPosition(4, 3, HexDirection.Bottom), []),
            new PathSegment(new HexPosition(4, 3, HexDirection.Bottom), new HexPosition(5, 3, HexDirection.Bottom), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 3));

        result.ShouldNotBeNull();
        result.ShouldStop.ShouldBeFalse();
        result.GameActions.ShouldHaveSingleItem();
        result.GameActions[0].ShouldBeOfType<PublishCommandAction>();
    }
}
