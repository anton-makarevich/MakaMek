using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public class MovementPhase(ServerGame game) : MainGamePhase(game)
{
    private Guid? _deferredMovementUnitId;
    private bool _requestDeferStepConsumption;

    public override void Enter()
    {
        ClearMovementDeferralState();
        base.Enter();
    }

    public override void Exit()
    {
        ClearMovementDeferralState();
        base.Exit();
    }

    private bool HasUnitMoved(Guid playerId, Guid unitId, string commandType)
    {
        var player = Game.Players.FirstOrDefault(p => p.Id == playerId);
        var unit = player?.Units.FirstOrDefault(u => u.Id == unitId);
        if (unit is not { HasMoved: true }) return false;
        Game.Logger.LogWarning(
            "Ignoring {CommandType} command for unit {UnitId} — movement already completed", commandType, unit.Id);
        return true;
    }

    public override void HandleCommand(IGameCommand command)
    {
        if (_deferredMovementUnitId is { } deferredId)
        {
            switch (command)
            {
                case MoveUnitCommand m when m.UnitId != deferredId:
                case TryStandupCommand t when t.UnitId != deferredId:
                    return;
            }
        }

        switch (command)
        {
            case MoveUnitCommand moveCommand:
                if (HasUnitMoved(moveCommand.PlayerId, moveCommand.UnitId, "MoveUnitCommand"))
                {
                    Game.CommandPublisher.PublishCommand(new ErrorCommand
                    {
                        GameOriginId = Game.Id,
                        IdempotencyKey = moveCommand.IdempotencyKey,
                        ErrorCode = ErrorCode.InvalidGameState,
                        Timestamp = DateTime.UtcNow
                    });
                    return;
                }
                
                HandleUnitAction(command, moveCommand.PlayerId);
                break;
            case TryStandupCommand standupCommand:
                if (HasUnitMoved(standupCommand.PlayerId, standupCommand.UnitId, "TryStandupCommand"))
                {
                    Game.CommandPublisher.PublishCommand(new ErrorCommand
                    {
                        GameOriginId = Game.Id,
                        IdempotencyKey = standupCommand.IdempotencyKey,
                        ErrorCode = ErrorCode.InvalidGameState,
                        Timestamp = DateTime.UtcNow
                    });
                    return;
                }
                
                ProcessStandupCommand(standupCommand);
                break;
        }
    }

    protected override bool ShouldFinalizeUnitsTurn(IGameCommand command)
    {
        if (command is not MoveUnitCommand m)
            return true;

        if (_requestDeferStepConsumption)
        {
            _requestDeferStepConsumption = false;
            _deferredMovementUnitId = m.UnitId;
            return false;
        }

        if (_deferredMovementUnitId == m.UnitId)
        {
            _deferredMovementUnitId = null;
        }

        return true;
    }

    protected override void ProcessCommand(IGameCommand command)
    {
        switch (command)
        {
            case MoveUnitCommand moveCommand:
                ProcessMoveCommand(moveCommand);
                break;
        }
    }

    private readonly IReadOnlyList<IMovementInterruptHandler> _segmentInterruptHandlers =
    [
        new BridgeCollapseInterruptHandler(),
        new SkidInterruptHandler(),
        new WaterEntryInterruptHandler()
    ];

    private readonly IReadOnlyList<IMovementInterruptHandler> _landingInterruptHandlers =
    [
        new BridgeCollapseInterruptHandler(),
        new JumpDamageInterruptHandler(),
        new WaterEntryInterruptHandler()
    ];

    private void ProcessMoveCommand(MoveUnitCommand moveCommand)
    {
        var player = Game.Players.FirstOrDefault(p => p.Id == moveCommand.PlayerId);
        var unit = player?.Units.FirstOrDefault(u => u.Id == moveCommand.UnitId);

        if (unit != null && moveCommand.MovementType != MovementType.Jump)
        {
            for (var i = 0; i < moveCommand.MovementPath.Count; i++)
            {
                foreach (var handler in _segmentInterruptHandlers)
                {
                    var context = new MovementInterruptContext
                    {
                        MoveCommand = moveCommand,
                        SegmentIndex = i,
                        Unit = unit,
                        Game = Game
                    };

                    var result = handler.Check(context);
                    if (result == null) continue;

                    if (ProcessInterruptResult(result, unit))
                        return;
                }
            }
        }

        // Normal completion — no interrupt fired
        Game.OnMoveUnit(moveCommand);
        var fullBroadcastCommand = moveCommand with
        {
            GameOriginId = Game.Id,
            IsCompleted = true
        };
        Game.CommandPublisher.PublishCommand(fullBroadcastCommand);

        // Jump landing: evaluate landing hex hazards
        if (moveCommand is { MovementType: MovementType.Jump, MovementPath.Count: > 0 })
        {
            var context = new MovementInterruptContext
            {
                MoveCommand = moveCommand,
                SegmentIndex = moveCommand.MovementPath.Count - 1,
                Unit = unit!,
                Game = Game,
                IsLandingCheck = true
            };

            foreach (var handler in _landingInterruptHandlers)
            {
                var result = handler.Check(context);
                if (result == null) continue;

                if (ProcessInterruptResult(result, unit!)) break;
            }
        }
    }

    private bool ProcessInterruptResult(MovementInterruptResult result, IUnit unit)
    {
        var commands = new List<IGameCommand>();
        bool? deferAfterFall = null;
        foreach (var action in result.GameActions)
        {
            commands.AddRange(action.Process(Game));
            if (unit is Mech fallingMech && action is ApplyFallAction)
                deferAfterFall = fallingMech.CanStandup();
        }

        foreach (var cmd in commands)
            Game.CommandPublisher.PublishCommand(cmd);

        if (!result.ShouldStop) return false;
        _requestDeferStepConsumption = deferAfterFall ?? result.DeferStepConsumption;
        return true;
    }

    private void ProcessStandupCommand(TryStandupCommand tryStandUpCommand)
    {
        // Find the unit
        var player = Game.Players.FirstOrDefault(p => p.Id == tryStandUpCommand.PlayerId);

        if (player?.Units.FirstOrDefault(u => u.Id == tryStandUpCommand.UnitId) is not Mech unit)
        {
            Game.CommandPublisher.PublishCommand(new ErrorCommand
            {
                GameOriginId = Game.Id,
                IdempotencyKey = tryStandUpCommand.IdempotencyKey,
                ErrorCode = ErrorCode.InvalidGameState,
                Timestamp = DateTime.UtcNow
            });
            Game.Logger.LogWarning("Unit not found");
            return;
        }
        
        var broadcastCommand = tryStandUpCommand with
        {
            GameOriginId = Game.Id
        };
        Game.CommandPublisher.PublishCommand(broadcastCommand);

        // Check if the unit can stand up (has sufficient MP, pilot is conscious, etc.)
        if (!unit.CanStandup() || unit.Position == null)
        {
            return; // Cannot stand up
        }

        // Use the FallProcessor to process the standup attempt and get context data
        var fallContextData = Game.FallProcessor.ProcessMovementAttempt(
            unit, new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt), Game, MovementType.StandingStill);
        
        var movementTypeAfterStandup = tryStandUpCommand.MovementTypeAfterStandup;
        
        // Create and publish the appropriate command based on the result
        if (fallContextData.IsFalling)
        {
            // Standup failed - fall
            var fallCommand = fallContextData.ToMechFallCommand();
            var action = new ApplyFallAction(unit, fallCommand);
            foreach (var cmd in action.Process(Game))
                Game.CommandPublisher.PublishCommand(cmd);
        }
        else
        {
            // Standup succeeded - stand up
            var standUpCommand = fallContextData.ToMechStandUpCommand(tryStandUpCommand.NewFacing, movementTypeAfterStandup);
            if (standUpCommand == null) return;
            Game.OnMechStandUp(standUpCommand.Value);
            Game.CommandPublisher.PublishCommand(standUpCommand);
        }
    }

    public override PhaseNames Name => PhaseNames.Movement;

    private void ClearMovementDeferralState()
    {
        _deferredMovementUnitId = null;
        _requestDeferStepConsumption = false;
    }
}
