using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
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

public class WaterEntryInterruptHandlerTests : GamePhaseTestsBase
{
    private readonly WaterEntryInterruptHandler _sut = new();
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
    public void WaterEntryInterruptHandler_Check_WhenShallowWater_ReturnsNull()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new WaterTerrain(0));

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Walk,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0)).ShouldBeNull();
    }

    [Fact]
    public void WaterEntryInterruptHandler_Check_WhenDeepWaterPass_ReturnsPsrOnly()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new WaterTerrain(-1));

        var successContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = false,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new EnteringDeepWaterRollContext(1),
                DiceResults = [10, 10],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            }
        };
        MockFallProcessor.ProcessMovementAttempt(mech, Arg.Any<EnteringDeepWaterRollContext>(), Game, MovementType.Walk)
            .Returns(successContext);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Walk,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0));

        result.ShouldNotBeNull();
        result.ShouldStop.ShouldBeFalse();
        result.GameActions.ShouldHaveSingleItem();
        result.GameActions[0].ShouldBeOfType<PublishCommandAction>();
    }

    [Fact]
    public void WaterEntryInterruptHandler_Check_WhenDeepWaterFall_ReturnsStopWithBroadcastAction()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new WaterTerrain(-1));

        var fallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new EnteringDeepWaterRollContext(1),
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
        MockFallProcessor.ProcessMovementAttempt(mech, Arg.Any<EnteringDeepWaterRollContext>(), Game, MovementType.Walk)
            .Returns(fallContext);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Walk,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0));

        result.ShouldNotBeNull();
        result.ShouldStop.ShouldBeTrue();
        result.GameActions.Count.ShouldBe(3);
        result.GameActions[0].ShouldBeOfType<MoveUnitAction>();
        result.GameActions[1].ShouldBeOfType<ApplyFallAction>();
        result.GameActions[2].ShouldBeOfType<WaterFallBroadcastAction>();
    }

    [Fact]
    public void WaterEntryInterruptHandler_Check_WhenSameCoordinates_ReturnsNull()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Walk,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(1, 2, HexDirection.Bottom), []));

        _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0)).ShouldBeNull();
    }

    [Fact]
    public void WaterEntryInterruptHandler_Check_WhenNoWaterTerrain_ReturnsNull()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        // No water terrain added - hex (2,2) is clear by default

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Walk,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0)).ShouldBeNull();
    }

    [Fact]
    public void WaterEntryInterruptHandler_Check_WhenLandingDeepWaterFall_ReturnsStopWithFallOnly()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(2, 2, HexDirection.Top), null);
        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new WaterTerrain(-1));

        var fallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new EnteringDeepWaterRollContext(1),
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
        MockFallProcessor.ProcessMovementAttempt(mech, Arg.Any<EnteringDeepWaterRollContext>(), Game, MovementType.Jump)
            .Returns(fallContext);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Jump,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        var context = new MovementInterruptContext
        {
            MoveCommand = moveCommand with { PlayerId = Game.Players[0].Id },
            SegmentIndex = 0,
            Unit = Game.Players[0].Units.Single(u => u.Id == moveCommand.UnitId),
            Game = Game,
            IsLandingCheck = true
        };

        var result = _sut.Check(context);

        result.ShouldNotBeNull();
        result.ShouldStop.ShouldBeTrue();
        result.GameActions.ShouldHaveSingleItem();
        result.GameActions[0].ShouldBeOfType<ApplyFallAction>();
    }
}
