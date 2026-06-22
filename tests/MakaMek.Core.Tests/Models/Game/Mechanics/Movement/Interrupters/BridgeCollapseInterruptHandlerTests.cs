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

public class BridgeCollapseInterruptHandlerTests : GamePhaseTestsBase
{
    private readonly BridgeCollapseInterruptHandler _sut = new();
    private Guid _unitId;
    private Mech _enteringMech = null!;
    private Mech _occupantMech = null!;
    private Mech _occupantMech2 = null!;

    protected override void SetupSut()
    {
        var playerId = Guid.NewGuid();
        Game.HandleCommand(CreateJoinCommand(playerId, "Player 1", unitsCount: 3));
        _unitId = Game.Players[0].Units[0].Id;
        _enteringMech = (Mech)Game.Players[0].Units[0];
        _occupantMech = (Mech)Game.Players[0].Units[1];
        _occupantMech2 = (Mech)Game.Players[0].Units[2];
        SetMap();
    }

    private void SetupFallContext(Mech mech, MovementType movementType)
    {
        var fallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            LevelsFallen = 2,
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3),
                HitDirection.Front)
        };
        MockFallProcessor.ProcessMovementAttempt(mech, Arg.Any<BridgeCollapseRollContext>(), Game, movementType)
            .Returns(fallContext);
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
    public void BridgeCollapseInterruptHandler_Check_WhenNoBridge_ReturnsNull()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Walk,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0)).ShouldBeNull();
    }

    [Fact]
    public void BridgeCollapseInterruptHandler_Check_WhenUnderWeight_ReturnsNull()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new BridgeTerrain(0, 100));

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Walk,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0)).ShouldBeNull();
    }

    [Fact]
    public void BridgeCollapseInterruptHandler_Check_WhenOverWeight_ReturnsStopWithActions()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new BridgeTerrain(2, 10));

        var fallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            LevelsFallen = 2,
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3),
                HitDirection.Front)
        };
        MockFallProcessor.ProcessMovementAttempt(mech, Arg.Any<BridgeCollapseRollContext>(), Game, MovementType.Walk)
            .Returns(fallContext);

        var idempotencyKey = Guid.NewGuid();
        var moveCommand = CreateMoveCommand(_unitId, MovementType.Walk,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));
        moveCommand = moveCommand with { IdempotencyKey = idempotencyKey };

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0));

        result.ShouldNotBeNull();
        result.ShouldStop.ShouldBeTrue();
        result.GameActions.ShouldContain(a => a is BridgeCollapsedAction);
        result.GameActions.ShouldContain(a => a is ApplyFallAction);

        // Verify the published MoveUnitCommand uses server GameOriginId and preserves IdempotencyKey
        var publishedMoveAction = result.GameActions
            .OfType<MoveUnitAction>()
            .FirstOrDefault(a => a.Command.IsCompleted);
        publishedMoveAction.ShouldNotBeNull();
        publishedMoveAction.Command.GameOriginId.ShouldBe(Game.Id);
        publishedMoveAction.Command.IdempotencyKey.ShouldBe(idempotencyKey);
    }

    [Fact]
    public void BridgeCollapseInterruptHandler_Check_WhenOverWeightAndFallReturnsFalse_ThrowsInvalidOperationException()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new BridgeTerrain(2, 10));

        var fallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = false
        };
        MockFallProcessor.ProcessMovementAttempt(mech, Arg.Any<BridgeCollapseRollContext>(), Game, MovementType.Walk)
            .Returns(fallContext);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Walk,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        Should.Throw<InvalidOperationException>(() =>
            _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0)));
    }

    [Fact]
    public void BridgeCollapseInterruptHandler_Check_WhenLandingUnderWeight_ReturnsNull()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(2, 2, HexDirection.Top), null);

        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new BridgeTerrain(2, 100));

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

        _sut.Check(context).ShouldBeNull();
    }

    [Fact]
    public void BridgeCollapseInterruptHandler_Check_WhenLandingOverWeight_ReturnsStopWithActions()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(2, 2, HexDirection.Top), null);

        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new BridgeTerrain(2, 10));

        var fallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = true,
            LevelsFallen = 2,
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3),
                HitDirection.Front)
        };
        MockFallProcessor.ProcessMovementAttempt(mech, Arg.Any<BridgeCollapseRollContext>(), Game, MovementType.Jump)
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
        result.GameActions.ShouldContain(a => a is BridgeCollapsedAction);
        result.GameActions.ShouldContain(a => a is ApplyFallAction);
    }

    [Fact]
    public void BridgeCollapseInterruptHandler_Check_WhenLandingOverWeightAndFallReturnsFalse_ThrowsInvalidOperationException()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(2, 2, HexDirection.Top), null);

        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new BridgeTerrain(2, 10));

        var fallContext = new FallContextData
        {
            UnitId = mech.Id,
            GameId = Game.Id,
            IsFalling = false
        };
        MockFallProcessor.ProcessMovementAttempt(mech, Arg.Any<BridgeCollapseRollContext>(), Game, MovementType.Jump)
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

        Should.Throw<InvalidOperationException>(() => _sut.Check(context));
    }

    // ── Domino / displacement tests ──────────────────────────────────────────

    [Fact]
    public void Check_WhenMultipleUnitsOnBridge_ShouldAddDisplacementAction()
    {
        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new BridgeTerrain(2, 10));

        _occupantMech.Deploy(new HexPosition(2, 2, HexDirection.Top),
            Game.BattleMap.GetHex(new HexCoordinates(2, 2)));
        _enteringMech.Deploy(new HexPosition(1, 2, HexDirection.Top),
            Game.BattleMap.GetHex(new HexCoordinates(1, 2)));

        SetupFallContext(_enteringMech, MovementType.Walk);
        SetupFallContext(_occupantMech, MovementType.StandingStill);

        var moveCommand = CreateMoveCommand(_enteringMech.Id, MovementType.Walk,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0));

        result.ShouldNotBeNull();
        result.GameActions.ShouldContain(a => a is DisplaceUnitAction);
    }

    [Fact]
    public void Check_ShouldDisplaceOccupantAwayFromEntryDirection()
    {
        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new BridgeTerrain(2, 10));

        _occupantMech.Deploy(new HexPosition(2, 2, HexDirection.Top),
            Game.BattleMap.GetHex(new HexCoordinates(2, 2)));
        _enteringMech.Deploy(new HexPosition(1, 2, HexDirection.Top),
            Game.BattleMap.GetHex(new HexCoordinates(1, 2)));

        SetupFallContext(_enteringMech, MovementType.Walk);
        SetupFallContext(_occupantMech, MovementType.StandingStill);

        var moveCommand = CreateMoveCommand(_enteringMech.Id, MovementType.Walk,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0));

        result.ShouldNotBeNull();
        result.GameActions.OfType<DisplaceUnitAction>().FirstOrDefault().ShouldNotBeNull();
    }

    [Fact]
    public void Check_WhenDisplacementOffMap_ShouldNotCreateDisplacementAction()
    {
        // Bridge at edge hex (1,1) so displacement target goes off-map (to Q=1, R=0)
        var bridgeHex = Game.BattleMap!.GetHex(new HexCoordinates(1, 1))!;
        bridgeHex.AddTerrain(new BridgeTerrain(2, 10));

        _occupantMech.Deploy(new HexPosition(1, 1, HexDirection.Top), bridgeHex);
        _enteringMech.Deploy(new HexPosition(1, 2, HexDirection.Top),
            Game.BattleMap.GetHex(new HexCoordinates(1, 2)));

        SetupFallContext(_enteringMech, MovementType.Walk);
        SetupFallContext(_occupantMech, MovementType.StandingStill);

        var moveCommand = CreateMoveCommand(_enteringMech.Id, MovementType.Walk,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(1, 1, HexDirection.Top), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0));

        result.ShouldNotBeNull();
        result.GameActions.ShouldNotContain(a => a is DisplaceUnitAction);
    }

    [Fact]
    public void Check_WhenSingleUnitOnBridge_ShouldNotAddDisplacement()
    {
        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new BridgeTerrain(2, 10));

        // Only deploy entering mech, leave occupants undeployed
        _enteringMech.Deploy(new HexPosition(1, 2, HexDirection.Top),
            Game.BattleMap.GetHex(new HexCoordinates(1, 2)));

        SetupFallContext(_enteringMech, MovementType.Walk);

        var moveCommand = CreateMoveCommand(_enteringMech.Id, MovementType.Walk,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0));

        result.ShouldNotBeNull();
        result.GameActions.ShouldNotContain(a => a is DisplaceUnitAction);
    }

    [Fact]
    public void Check_DisplacementActionsShouldAppearAfterFallActions()
    {
        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new BridgeTerrain(2, 10));

        _occupantMech.Deploy(new HexPosition(2, 2, HexDirection.Top),
            Game.BattleMap.GetHex(new HexCoordinates(2, 2)));
        _enteringMech.Deploy(new HexPosition(1, 2, HexDirection.Top),
            Game.BattleMap.GetHex(new HexCoordinates(1, 2)));

        SetupFallContext(_enteringMech, MovementType.Walk);
        SetupFallContext(_occupantMech, MovementType.StandingStill);

        var moveCommand = CreateMoveCommand(_enteringMech.Id, MovementType.Walk,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0));

        result.ShouldNotBeNull();
        var actions = result.GameActions.ToList();
        var lastFallIndex = actions.FindLastIndex(a => a is ApplyFallAction);
        var firstDisplaceIndex = actions.FindIndex(a => a is DisplaceUnitAction);
        firstDisplaceIndex.ShouldBeGreaterThan(lastFallIndex);
    }

    [Fact]
    public void Check_LandingWithMultipleUnits_ShouldAddDisplacement()
    {
        var bridgeHex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!;
        bridgeHex.AddTerrain(new BridgeTerrain(2, 10));

        // Both mechs on the bridge hex (jumping unit already landed)
        _occupantMech.Deploy(new HexPosition(2, 2, HexDirection.Top), bridgeHex);
        _enteringMech.Deploy(new HexPosition(2, 2, HexDirection.Bottom), bridgeHex);

        SetupFallContext(_enteringMech, MovementType.Jump);
        SetupFallContext(_occupantMech, MovementType.StandingStill);

        var moveCommand = CreateMoveCommand(_enteringMech.Id, MovementType.Jump,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        var context = new MovementInterruptContext
        {
            MoveCommand = moveCommand with { PlayerId = Game.Players[0].Id },
            SegmentIndex = 0,
            Unit = _enteringMech,
            Game = Game,
            IsLandingCheck = true
        };

        var result = _sut.Check(context);

        result.ShouldNotBeNull();
        result.GameActions.ShouldContain(a => a is DisplaceUnitAction);
    }

    [Fact]
    public void Check_LandingSingleUnit_ShouldNotAddDisplacement()
    {
        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new BridgeTerrain(2, 10));

        _enteringMech.Deploy(new HexPosition(2, 2, HexDirection.Top),
            Game.BattleMap.GetHex(new HexCoordinates(2, 2)));

        SetupFallContext(_enteringMech, MovementType.Jump);

        var moveCommand = CreateMoveCommand(_enteringMech.Id, MovementType.Jump,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        var context = new MovementInterruptContext
        {
            MoveCommand = moveCommand with { PlayerId = Game.Players[0].Id },
            SegmentIndex = 0,
            Unit = _enteringMech,
            Game = Game,
            IsLandingCheck = true
        };

        var result = _sut.Check(context);

        result.ShouldNotBeNull();
        result.GameActions.ShouldNotContain(a => a is DisplaceUnitAction);
    }

    [Fact]
    public void Check_WithMultipleBridgeOccupants_ShouldDisplaceToDistinctTargets()
    {
        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new BridgeTerrain(2, 10));

        // Both occupants on the bridge hex
        _occupantMech.Deploy(new HexPosition(2, 2, HexDirection.Top),
            Game.BattleMap.GetHex(new HexCoordinates(2, 2)));
        _occupantMech2.Deploy(new HexPosition(2, 2, HexDirection.Top),
            Game.BattleMap.GetHex(new HexCoordinates(2, 2)));
        _enteringMech.Deploy(new HexPosition(1, 2, HexDirection.Top),
            Game.BattleMap.GetHex(new HexCoordinates(1, 2)));

        SetupFallContext(_enteringMech, MovementType.Walk);
        SetupFallContext(_occupantMech, MovementType.StandingStill);
        SetupFallContext(_occupantMech2, MovementType.StandingStill);

        var moveCommand = CreateMoveCommand(_enteringMech.Id, MovementType.Walk,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0));

        result.ShouldNotBeNull();
        var displaceActions = result.GameActions.OfType<DisplaceUnitAction>().ToList();
        displaceActions.ShouldNotBeEmpty();
        // With multiple occupants sharing the bridge hex, only one should be displaced
        // since both would target the same hex; reservedTargets prevents duplicates
        displaceActions.Count.ShouldBe(1);
    }

    [Fact]
    public void Check_WhenDisplacedUnitLandsOnOccupiedHex_ShouldChainDisplacement()
    {
        // Bridge at (2,2). Entering from (1,2) → (2,2).
        // Entry direction from bridge back to (1,2) = TopLeft → displacement direction = BottomRight.
        // Occupant1 at (2,2) → displaced to (3,3).
        // Occupant2 already at (3,3) → chained to (4,3).
        // This exercises lines 217-219 (occupant != null → queue.Enqueue).
        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new BridgeTerrain(2, 10));

        _occupantMech.Deploy(new HexPosition(2, 2, HexDirection.Top),
            Game.BattleMap.GetHex(new HexCoordinates(2, 2)));
        _occupantMech2.Deploy(new HexPosition(3, 3, HexDirection.Top),
            Game.BattleMap.GetHex(new HexCoordinates(3, 3)));
        _enteringMech.Deploy(new HexPosition(1, 2, HexDirection.Top),
            Game.BattleMap.GetHex(new HexCoordinates(1, 2)));

        SetupFallContext(_enteringMech, MovementType.Walk);
        SetupFallContext(_occupantMech, MovementType.StandingStill);

        var moveCommand = CreateMoveCommand(_enteringMech.Id, MovementType.Walk,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []));

        var result = _sut.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, 0));

        result.ShouldNotBeNull();
        var displaceActions = result.GameActions.OfType<DisplaceUnitAction>().ToList();
        // Occupant1 displaced from (2,2) → (3,3); occupant2 chained from (3,3) → (4,3)
        displaceActions.Count.ShouldBe(2);
    }
}
