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

public class MovementInterruptHandlerTests : GamePhaseTestsBase
{
    private readonly IReadOnlyList<IMovementInterruptHandler> _handlers =
    [
        new BridgeCollapseInterruptHandler(),
        new SkidInterruptHandler(),
        new WaterEntryInterruptHandler()
    ];

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
    public void HandlerLoop_WhenBridgeAtEarlierSegmentThanWater_ShouldStopAtBridge()
    {
        var mech = Game.Players[0].Units[0] as Mech;
        mech!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        Game.BattleMap!.GetHex(new HexCoordinates(2, 2))!.AddTerrain(new BridgeTerrain(2, 10));
        Game.BattleMap!.GetHex(new HexCoordinates(3, 2))!.AddTerrain(new WaterTerrain(-1));

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
        MockFallProcessor.ProcessMovementAttempt(mech, Arg.Any<EnteringDeepWaterRollContext>(), Game, MovementType.Walk)
            .Returns(fallContext);

        var moveCommand = CreateMoveCommand(_unitId, MovementType.Walk,
            new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), []),
            new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), []));

        MovementInterruptResult? triggeredResult = null;
        for (var i = 0; i < moveCommand.MovementPath.Count; i++)
        {
            foreach (var handler in _handlers)
            {
                var result = handler.Check(CreateContext(moveCommand with { PlayerId = Game.Players[0].Id }, i));
                if (result == null) continue;
                triggeredResult = result;
                if (result.ShouldStop) goto done;
            }
        }

        done:
        triggeredResult.ShouldNotBeNull();
        triggeredResult.GameActions.ShouldContain(a => a is BridgeCollapsedAction);
        triggeredResult.GameActions.ShouldNotContain(a => a is WaterFallBroadcastAction);
    }
}
