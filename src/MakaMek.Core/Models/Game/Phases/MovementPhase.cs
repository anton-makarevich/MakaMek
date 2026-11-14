using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public class MovementPhase(
    ServerGame game,
    DominoEffectResolver dominoEffectResolver,
    IMovementAttackDeclarationValidator? declarationValidator = null)
    : MainGamePhase(game)
{
    private readonly IMovementAttackDeclarationValidator _declarationValidator =
        declarationValidator ?? game.MovementAttackDeclarationValidator;
    private readonly MovementInterruptRunner _interruptRunner = new(game, dominoEffectResolver);
    private readonly MovementStandupResolver _standupResolver = new(game);
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
            var interrupt = _interruptRunner.RunSegmentInterrupts(unit, interruptCommand);
            if (interrupt is { } stoppingOutcome)
            {
                _requestDeferStepConsumption = stoppingOutcome.DeferStepConsumption;
                return;
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
            var landingInterrupt = _interruptRunner.RunLandingInterrupts(unit!, interruptCommand);
            if (landingInterrupt is { } landingOutcome)
                _requestDeferStepConsumption = landingOutcome.DeferStepConsumption;
        }
    }

    private void ProcessStandupCommand(TryStandupCommand tryStandUpCommand)
    {
        _standupResolver.Process(tryStandUpCommand);
    }

    public override PhaseNames Name => PhaseNames.Movement;

    private void ClearMovementDeferralState()
    {
        _deferredMovementUnitId = null;
        _requestDeferStepConsumption = false;
    }
}
