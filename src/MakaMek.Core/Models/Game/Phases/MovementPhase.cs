using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public class MovementPhase(ServerGame game, IMovementAttackDeclarationValidator? declarationValidator = null)
    : MainGamePhase(game)
{
    private readonly IMovementAttackDeclarationValidator _declarationValidator =
        declarationValidator ?? new MovementAttackDeclarationValidator();
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

                if (ValidateMovementAttack(moveCommand) is not { } commandToProcess) return;

                HandleUnitAction(commandToProcess, moveCommand.PlayerId);
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

    /// <summary>
    /// Validates a Charge or DFA carried by the move and returns the command to process, with the
    /// server-computed declaration, or null when the command was rejected. A rejected declaration
    /// leaves the unit where it was, so it can submit another move.
    /// </summary>
    private MoveUnitCommand? ValidateMovementAttack(MoveUnitCommand command)
    {
        if (command.Declaration is not { } declaration) return command;

        var player = Game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        var unit = player?.Units.FirstOrDefault(u => u.Id == command.UnitId);
        var result = unit is null || Game.BattleMap is null || !command.IsCompleted
            ? MovementAttackDeclarationValidation.Invalid(ErrorCode.ValidationFailed)
            : _declarationValidator.Validate(unit, command.MovementPath, command.MovementType,
                declaration, Game.Players.SelectMany(p => p.Units), Game.BattleMap);

        if (result.IsValid) return command with { Declaration = result.Declaration };

        Game.Logger.LogWarning(
            "Rejecting {AttackType} declaration for unit {UnitId}: {ErrorCode}",
            declaration.AttackType, command.UnitId, result.ErrorCode);
        Game.CommandPublisher.PublishCommand(new ErrorCommand
        {
            GameOriginId = Game.Id,
            IdempotencyKey = command.IdempotencyKey,
            ErrorCode = result.ErrorCode ?? ErrorCode.ValidationFailed,
            Timestamp = DateTime.UtcNow
        });
        return null;
    }

    private readonly IReadOnlyList<IMovementInterruptHandler> _segmentInterruptHandlers =
    [
        new BridgeCollapseInterruptHandler(),
        new SkidInterruptHandler(),
        new WaterEntryInterruptHandler(),
        new RubbleEntryInterruptHandler()
    ];

    private readonly IReadOnlyList<IMovementInterruptHandler> _landingInterruptHandlers =
    [
        new BridgeCollapseInterruptHandler(),
        new JumpDamageInterruptHandler(),
        new WaterEntryInterruptHandler(),
        new RubbleEntryInterruptHandler()
    ];

    private void ProcessMoveCommand(MoveUnitCommand moveCommand)
    {
        var player = Game.Players.FirstOrDefault(p => p.Id == moveCommand.PlayerId);
        var unit = player?.Units.FirstOrDefault(u => u.Id == moveCommand.UnitId);

        // The interrupts and the actions they produce describe where the unit actually got to. A
        // declaration on the original command must not ride along into a truncated or skidding
        // move, which is never a completed charge or DFA.
        var interruptCommand = moveCommand with { Declaration = null };

        if (unit != null && moveCommand.MovementType != MovementType.Jump)
        {
            for (var i = 0; i < moveCommand.MovementPath.Count; i++)
            {
                foreach (var handler in _segmentInterruptHandlers)
                {
                    var context = new MovementInterruptContext
                    {
                        MoveCommand = interruptCommand,
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
                MoveCommand = interruptCommand,
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
            if (action is ApplySkidAction)
                deferAfterFall = false;
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
