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

    private static void SetHexLevel(IBattleMap map, HexCoordinates coords, int level)
    {
        var existingHex = map.GetHex(coords);
        var newHex = new Hex(coords, level);
        foreach (var terrain in existingHex!.GetTerrains())
            newHex.AddTerrain(terrain);
        map.AddHex(newHex);
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

    // ──────────────────────────────────────────────
    // Cliff detection tests
    // ──────────────────────────────────────────────

    [Fact]
    public void SkidInterruptHandler_Check_WhenElevationDropExceedsMaxLevelChange_CliffDetected()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        Game.BattleMap!.GetHex(new HexCoordinates(3, 2))!.AddTerrain(new RoadTerrain());

        // Set elevation so first skid hex has a 3-level drop (exceeds Mech.MaxLevelChangeForward=2)
        SetHexLevel(Game.BattleMap!, new HexCoordinates(3, 2), 3);
        SetHexLevel(Game.BattleMap!, new HexCoordinates(3, 1), 0);

        var initialFallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(0, 2, 3),
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
            Arg.Is<SkidCheckRollContext>(ctx => ctx.SkidDistance == 1),
            Game,
            MovementType.Run)
            .Returns(initialFallContext);

        var cliffFallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 10),
                new DiceResult(3),
                HitDirection.Front),
            LevelsFallen = 3,
            PilotDamagePilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new PilotDamageFromFallRollContext(3),
                DiceResults = [7, 7],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 5, Modifiers = [] }
            }
        };
        MockFallProcessor.ProcessMovementAttempt(
            mech,
            Arg.Is<CliffFallRollContext>(ctx => ctx.LevelsFallen == 3),
            Game,
            MovementType.Run)
            .Returns(cliffFallContext);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Run,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []),
            new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), []),
            new PathSegment(new HexPosition(3, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Bottom), []),
            new PathSegment(new HexPosition(3, 2, HexDirection.Bottom), new HexPosition(4, 2, HexDirection.Bottom), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 2));

        result.ShouldNotBeNull();
        result.ShouldStop.ShouldBeTrue();
        result.GameActions.ShouldContain(a => a is MoveUnitAction);
        result.GameActions.Count(a => a is ApplyFallAction).ShouldBe(2);
    }

    [Fact]
    public void SkidInterruptHandler_Check_CliffLevelsMatchElevationChange()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        Game.BattleMap!.GetHex(new HexCoordinates(3, 2))!.AddTerrain(new RoadTerrain());

        // 5-level drop
        SetHexLevel(Game.BattleMap!, new HexCoordinates(3, 2), 5);
        SetHexLevel(Game.BattleMap!, new HexCoordinates(3, 1), 0);

        var initialFallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(0, 2, 5),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(4),
                HitDirection.Front)
        };
        MockFallProcessor.ProcessMovementAttempt(
            mech,
            Arg.Is<SkidCheckRollContext>(ctx => ctx.SkidDistance == 1),
            Game,
            MovementType.Run)
            .Returns(initialFallContext);

        var cliffFallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 15),
                new DiceResult(4),
                HitDirection.Front),
            LevelsFallen = 5
        };
        MockFallProcessor.ProcessMovementAttempt(
            mech,
            Arg.Is<CliffFallRollContext>(ctx => ctx.LevelsFallen == 5),
            Game,
            MovementType.Run)
            .Returns(cliffFallContext);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Run,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []),
            new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), []),
            new PathSegment(new HexPosition(3, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Bottom), []),
            new PathSegment(new HexPosition(3, 2, HexDirection.Bottom), new HexPosition(4, 2, HexDirection.Bottom), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 2));

        result.ShouldNotBeNull();
        result.ShouldStop.ShouldBeTrue();
        MockFallProcessor.Received(1).ProcessMovementAttempt(
            mech,
            Arg.Is<CliffFallRollContext>(ctx => ctx.LevelsFallen == 5),
            Game,
            MovementType.Run);
    }

    [Fact]
    public void SkidInterruptHandler_Check_WhenCliffAndPsrSucceeds_DoesNotThrowNullReference()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        Game.BattleMap!.GetHex(new HexCoordinates(3, 2))!.AddTerrain(new RoadTerrain());

        // Set elevation so first skid hex has a 3-level drop (exceeds Mech.MaxLevelChangeForward=2)
        SetHexLevel(Game.BattleMap!, new HexCoordinates(3, 2), 3);
        SetHexLevel(Game.BattleMap!, new HexCoordinates(3, 1), 0);

        // PSR succeeds: IsFalling=false, no FallingDamageData
        var successContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = false,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(1, 2),
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
    public void SkidInterruptHandler_Check_NoCliffWhenElevationChangeWithinLimits()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        Game.BattleMap!.GetHex(new HexCoordinates(3, 2))!.AddTerrain(new RoadTerrain());

        // 1-level drop is within Mech.MaxLevelChangeForward=2
        SetHexLevel(Game.BattleMap!, new HexCoordinates(3, 2), 1);
        SetHexLevel(Game.BattleMap!, new HexCoordinates(3, 1), 0);

        var successContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = false,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(1, 2),
                DiceResults = [10, 10],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            }
        };
        MockFallProcessor.ProcessMovementAttempt(
            mech,
            Arg.Is<SkidCheckRollContext>(ctx => ctx.SkidDistance == 1 && ctx.AccidentalFallLevels == 0),
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
        MockFallProcessor.Received(1).ProcessMovementAttempt(
            mech,
            Arg.Is<SkidCheckRollContext>(ctx => ctx.SkidDistance == 1 && ctx.AccidentalFallLevels == 0),
            Game,
            MovementType.Run);
    }

    // ──────────────────────────────────────────────
    // Three-command sequence tests
    // ──────────────────────────────────────────────

    [Fact]
    public void SkidInterruptHandler_Check_CliffFallReturnsThreeCommandsInOrder()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 3, HexDirection.Top), null);
        Game.BattleMap!.GetHex(new HexCoordinates(4, 3))!.AddTerrain(new RoadTerrain());

        // Create setup where skid has one normal step before the cliff (hexesMoved=3, maxSkidDistance=2)
        SetHexLevel(Game.BattleMap!, new HexCoordinates(4, 3), 3);
        SetHexLevel(Game.BattleMap!, new HexCoordinates(4, 2), 3);  // No cliff
        SetHexLevel(Game.BattleMap!, new HexCoordinates(4, 1), 0);  // 3-level drop → cliff

        var initialFallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(1, 3, 3),
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
                new HitLocationsData([], 2),
                new DiceResult(3),
                HitDirection.Front),
            SkidDistance = 1
        };
        MockFallProcessor.ProcessMovementAttempt(
            mech,
            Arg.Is<SkidCheckRollContext>(ctx => ctx.SkidDistance == 2),
            Game,
            MovementType.Run)
            .Returns(initialFallContext);

        var cliffFallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 10),
                new DiceResult(3),
                HitDirection.Front),
            LevelsFallen = 3,
            PilotDamagePilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new PilotDamageFromFallRollContext(3),
                DiceResults = [7, 7],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 5, Modifiers = [] }
            }
        };
        MockFallProcessor.ProcessMovementAttempt(
            mech,
            Arg.Is<CliffFallRollContext>(ctx => ctx.LevelsFallen == 3),
            Game,
            MovementType.Run)
            .Returns(cliffFallContext);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Run,
            new PathSegment(new HexPosition(1, 3, HexDirection.Top), new HexPosition(2, 3, HexDirection.Top), []),
            new PathSegment(new HexPosition(2, 3, HexDirection.Top), new HexPosition(3, 3, HexDirection.Top), []),
            new PathSegment(new HexPosition(3, 3, HexDirection.Top), new HexPosition(4, 3, HexDirection.Top), []),
            new PathSegment(new HexPosition(4, 3, HexDirection.Top), new HexPosition(4, 3, HexDirection.Bottom), []),
            new PathSegment(new HexPosition(4, 3, HexDirection.Bottom), new HexPosition(5, 3, HexDirection.Bottom), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 3));

        result.ShouldNotBeNull();
        result.ShouldStop.ShouldBeTrue();
        result.GameActions.Count.ShouldBe(4); // MoveUnitAction + ApplyFallAction + ApplySkidAction + ApplyFallAction
        result.GameActions[0].ShouldBeOfType<MoveUnitAction>();
        result.GameActions[1].ShouldBeOfType<ApplyFallAction>();
        result.GameActions[2].ShouldBeOfType<ApplySkidAction>();
        result.GameActions[3].ShouldBeOfType<ApplyFallAction>();
    }

    [Fact]
    public void SkidInterruptHandler_Check_CliffFall_SharedFacingBetweenBothFalls()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 3, HexDirection.Top), null);
        Game.BattleMap!.GetHex(new HexCoordinates(4, 3))!.AddTerrain(new RoadTerrain());
        SetHexLevel(Game.BattleMap!, new HexCoordinates(4, 3), 3);
        SetHexLevel(Game.BattleMap!, new HexCoordinates(4, 2), 3);

        var sharedFacingRoll = new DiceResult(3);
        const HexDirection sharedFacing = HexDirection.Top;

        var initialFallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(1, 3, 3),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                sharedFacing,
                new HitLocationsData([], 5),
                sharedFacingRoll,
                HitDirection.Front),
            SkidDamageData = new FallingDamageData(
                sharedFacing,
                new HitLocationsData([], 2),
                sharedFacingRoll,
                HitDirection.Front),
            SkidDistance = 1,
            LevelsFallen = 0,
            PilotDamagePilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new PilotDamageFromFallRollContext(0),
                DiceResults = [7, 7],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            }
        };
        MockFallProcessor.ProcessMovementAttempt(
            mech,
            Arg.Any<SkidCheckRollContext>(),
            Game,
            MovementType.Run)
            .Returns(initialFallContext);

        var cliffFallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            FallingDamageData = new FallingDamageData(
                sharedFacing,
                new HitLocationsData([], 10),
                sharedFacingRoll,
                HitDirection.Front),
            LevelsFallen = 3,
            PilotDamagePilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new PilotDamageFromFallRollContext(3),
                DiceResults = [7, 7],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 5, Modifiers = [] }
            }
        };
        MockFallProcessor.ProcessMovementAttempt(
            mech,
            Arg.Is<CliffFallRollContext>(ctx => ctx.LevelsFallen == 3),
            Game,
            MovementType.Run)
            .Returns(cliffFallContext);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Run,
            new PathSegment(new HexPosition(1, 3, HexDirection.Top), new HexPosition(2, 3, HexDirection.Top), []),
            new PathSegment(new HexPosition(2, 3, HexDirection.Top), new HexPosition(3, 3, HexDirection.Top), []),
            new PathSegment(new HexPosition(3, 3, HexDirection.Top), new HexPosition(4, 3, HexDirection.Top), []),
            new PathSegment(new HexPosition(4, 3, HexDirection.Top), new HexPosition(4, 3, HexDirection.Bottom), []),
            new PathSegment(new HexPosition(4, 3, HexDirection.Bottom), new HexPosition(5, 3, HexDirection.Bottom), []));

        _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 3));

        MockFallProcessor.Received(1).ProcessMovementAttempt(
            mech,
            Arg.Is<CliffFallRollContext>(ctx => ctx.LevelsFallen == 3),
            Game,
            MovementType.Run);
    }

    [Fact]
    public void SkidInterruptHandler_Check_CliffFall_SkidDistanceReflectsPreCliffHexes()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 4, HexDirection.Top), null);

        // Use turn at (4,4) with 3 hexes moved before → maxSkidDistance=2
        // Skid goes Top from (4,4): pre-cliff at (4,3), cliff at (4,2)
        Game.BattleMap!.GetHex(new HexCoordinates(4, 4))!.AddTerrain(new RoadTerrain());
        SetHexLevel(Game.BattleMap!, new HexCoordinates(4, 4), 3);
        SetHexLevel(Game.BattleMap!, new HexCoordinates(4, 3), 3);  // pre-cliff, same level
        SetHexLevel(Game.BattleMap!, new HexCoordinates(4, 2), 0);  // cliff (3-level drop)

        var initialFallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(1, 3, 3),
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
                new HitLocationsData([], 2),
                new DiceResult(3),
                HitDirection.Front),
            SkidDistance = 1,
            LevelsFallen = 0
        };
        MockFallProcessor.ProcessMovementAttempt(
            mech,
            Arg.Is<SkidCheckRollContext>(ctx => ctx.SkidDistance == 2),
            Game,
            MovementType.Run)
            .Returns(initialFallContext);

        var cliffFallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 10),
                new DiceResult(3),
                HitDirection.Front),
            LevelsFallen = 3
        };
        MockFallProcessor.ProcessMovementAttempt(
            mech,
            Arg.Is<CliffFallRollContext>(ctx => ctx.LevelsFallen == 3),
            Game,
            MovementType.Run)
            .Returns(cliffFallContext);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Run,
            new PathSegment(new HexPosition(1, 4, HexDirection.Top), new HexPosition(2, 4, HexDirection.Top), []),
            new PathSegment(new HexPosition(2, 4, HexDirection.Top), new HexPosition(3, 4, HexDirection.Top), []),
            new PathSegment(new HexPosition(3, 4, HexDirection.Top), new HexPosition(4, 4, HexDirection.Top), []),
            new PathSegment(new HexPosition(4, 4, HexDirection.Top), new HexPosition(4, 4, HexDirection.Bottom), []),
            new PathSegment(new HexPosition(4, 4, HexDirection.Bottom), new HexPosition(5, 4, HexDirection.Bottom), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 3));

        result.ShouldNotBeNull();
        result.ShouldStop.ShouldBeTrue();

        // Verify skid context was created with correct distance
        MockFallProcessor.Received(1).ProcessMovementAttempt(
            mech,
            Arg.Is<SkidCheckRollContext>(ctx => ctx.SkidDistance == 2),
            Game,
            MovementType.Run);
    }

    [Fact]
    public void SkidInterruptHandler_Check_CliffFall_EachFallHasIndependentPilotDamagePsr()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        Game.BattleMap!.GetHex(new HexCoordinates(3, 2))!.AddTerrain(new RoadTerrain());
        SetHexLevel(Game.BattleMap!, new HexCoordinates(3, 2), 3);
        SetHexLevel(Game.BattleMap!, new HexCoordinates(3, 1), 0);

        // Initial fall has a failed pilot damage PSR → pilot injured
        var initialFallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(0, 2, 3),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3),
                HitDirection.Front),
            PilotDamagePilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new PilotDamageFromFallRollContext(0),
                DiceResults = [4, 4],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            }
        };
        MockFallProcessor.ProcessMovementAttempt(
            mech,
            Arg.Is<SkidCheckRollContext>(ctx => ctx.SkidDistance == 1),
            Game,
            MovementType.Run)
            .Returns(initialFallContext);

        // Cliff fall has a successful pilot damage PSR → no pilot injury
        var cliffFallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 10),
                new DiceResult(3),
                HitDirection.Front),
            LevelsFallen = 3,
            PilotDamagePilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new PilotDamageFromFallRollContext(3),
                DiceResults = [7, 7],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 5, Modifiers = [] }
            }
        };
        MockFallProcessor.ProcessMovementAttempt(
            mech,
            Arg.Is<CliffFallRollContext>(ctx => ctx.LevelsFallen == 3),
            Game,
            MovementType.Run)
            .Returns(cliffFallContext);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Run,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []),
            new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), []),
            new PathSegment(new HexPosition(3, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Bottom), []),
            new PathSegment(new HexPosition(3, 2, HexDirection.Bottom), new HexPosition(4, 2, HexDirection.Bottom), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 2));

        result.ShouldNotBeNull();
        result.ShouldStop.ShouldBeTrue();
        result.GameActions.Count.ShouldBe(3); // MoveUnitAction + initial fall + cliff fall

        // Verify both fall commands exist
        result.GameActions.Count(a => a is ApplyFallAction).ShouldBe(2);

        // Verify initial fall has IsPilotTakingDamage=true
        var initialFallCommand = initialFallContext.ToMechFallCommand();
        initialFallCommand.IsPilotTakingDamage.ShouldBeTrue();

        // Verify cliff fall does NOT have pilot taking damage
        var cliffFallCommand = cliffFallContext.ToMechFallCommand();
        cliffFallCommand.IsPilotTakingDamage.ShouldBeFalse();
    }
}
