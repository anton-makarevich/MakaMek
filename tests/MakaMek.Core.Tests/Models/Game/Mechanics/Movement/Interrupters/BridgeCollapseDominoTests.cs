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

public class BridgeCollapseDominoTests : GamePhaseTestsBase
{
    private readonly BridgeCollapseInterruptHandler _sut = new();
    private Mech _enteringMech = null!;
    private Mech _occupantMech = null!;

    protected override void SetupSut()
    {
        var playerId = Guid.NewGuid();
        // Join with 2 units for multi-unit tests
        Game.HandleCommand(CreateJoinCommand(playerId, "Player 1", unitsCount: 2));
        _enteringMech = (Mech)Game.Players[0].Units[0];
        _occupantMech = (Mech)Game.Players[0].Units[1];
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
        var displaceAction = result.GameActions.OfType<DisplaceUnitAction>().FirstOrDefault();
        displaceAction.ShouldNotBeNull();
    }

    [Fact]
    public void Check_WhenDisplacementOffMap_ShouldNotCreateDisplacementAction()
    {
        // Bridge at edge hex (1, 1) so displacement target goes off-map (to Q=1, R=0)
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

        // Only deploy entering mech, leave occupant undeployed
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
}
