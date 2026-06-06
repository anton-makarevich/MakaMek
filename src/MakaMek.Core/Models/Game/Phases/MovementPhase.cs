using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

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

    private readonly IReadOnlyList<IMovementInterruptHandler> _interruptHandlers =
    [
        new BridgeCollapseInterruptHandler(),
        new SkidInterruptHandler(),
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
                foreach (var handler in _interruptHandlers)
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

                    // Apply state mutations and collect commands
                    var commands = new List<IGameCommand>();
                    bool? deferAfterFall = null;
                    foreach (var action in result.GameActions)
                    {
                        commands.AddRange(action.Process(Game));
                        if (unit is Mech fallingMech && action is ApplyFallAction)
                            deferAfterFall = fallingMech.CanStandup();
                    }

                    // Phase publishes all commands
                    foreach (var cmd in commands)
                        Game.CommandPublisher.PublishCommand(cmd);

                    if (!result.ShouldStop) continue;
                    _requestDeferStepConsumption = deferAfterFall ?? result.DeferStepConsumption;
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

        // Jump landing logic
        if (unit is Mech mech && moveCommand.MovementType == MovementType.Jump)
        {
            var fell = false;
            if (mech.IsPsrForJumpRequired())
            {
                fell = ProcessJumpWithDamage(mech);
            }
            
            if (moveCommand.MovementPath.Count > 0)
            {
                var lastSegment = moveCommand.MovementPath.Last();
                var landingCoords = new HexCoordinates(lastSegment.To.Coordinates);
                var destHex = Game.BattleMap?.GetHex(landingCoords);

                var bridgeCollapsed = false;

                if (destHex?.GetTerrain(MakaMekTerrains.Bridge) is BridgeTerrain bridgeTerrain)
                {
                    var bridgeHeight = bridgeTerrain.Height;
                    var constructionFactor = bridgeTerrain.ConstructionFactor;

                    var unitsOnHex = Game.Players
                        .SelectMany(p => p.Units)
                        .Where(u => u.IsDeployed && u.Position!.Coordinates == landingCoords)
                        .ToList();
                    var totalTonnage = unitsOnHex.Sum(u => u.Tonnage);
                    if (totalTonnage > constructionFactor)
                    {
                        bridgeCollapsed = true;

                        var bridgeCommand = new BridgeCollapsedCommand
                        {
                            GameOriginId = Game.Id,
                            Coordinates = landingCoords.ToData(),
                            ConstructionFactor = constructionFactor,
                            TotalTonnage = totalTonnage,
                            TriggeringUnitId = mech.Id,
                            Timestamp = DateTime.UtcNow
                        };
                        Game.OnBridgeCollapsed(bridgeCommand);
                        Game.CommandPublisher.PublishCommand(bridgeCommand);

                        foreach (var hexUnit in unitsOnHex)
                        {
                            if (hexUnit is not Mech hexMech) continue;
                            if (fell && hexUnit.Id == moveCommand.UnitId) continue;

                            var movementType = hexUnit.Id == moveCommand.UnitId
                                ? moveCommand.MovementType
                                : MovementType.StandingStill;
                            var fcData = Game.FallProcessor.ProcessMovementAttempt(
                                hexMech, new BridgeCollapseRollContext(bridgeHeight), Game, movementType);

                            if (fcData.IsFalling)
                            {
                                var fallCmd = fcData.ToMechFallCommand();
                                ProcessFallCommand(fallCmd, hexMech, false);
                                Game.CommandPublisher.PublishCommand(fallCmd);
                            }
                            else
                            {
                                Game.Logger.LogError(
                                    "Bridge collapse should always result in a fall for unit {UnitId}", hexMech.Id);
                                throw new InvalidOperationException(
                                    $"Bridge collapse should always result in a fall for unit {hexMech.Id}");
                            }
                        }
                    }
                }

                if (!fell && !bridgeCollapsed && destHex?.GetTerrain(MakaMekTerrains.Water) is WaterTerrain { Height: <= -1 } water)
                {
                    var fallContextData = Game.FallProcessor.ProcessMovementAttempt(
                        mech, new EnteringDeepWaterRollContext(-1*water.Height), Game, MovementType.Jump);
                    if (fallContextData.IsFalling)
                    {
                        var fallCommand = fallContextData.ToMechFallCommand();
                        ProcessFallCommand(fallCommand, mech);
                    }
                    else
                    {
                        var psrCommand = fallContextData.ToMechFallCommand();
                        Game.CommandPublisher.PublishCommand(psrCommand);
                    }
                }
            }
        }
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
            ProcessFallCommand(fallCommand, unit);
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

    private bool ProcessJumpWithDamage(Unit? unit)
    {
        // Use the FallProcessor to process the jump attempt with damaged gyro
        if (unit is not Mech mech) return false;
        var fallContextData = Game.FallProcessor.ProcessMovementAttempt(
            mech, new PilotingSkillRollContext(PilotingSkillRollType.JumpWithDamage), Game, MovementType.Jump);
        
        if (!fallContextData.IsFalling)
        {
            var psrCommand = fallContextData.ToMechFallCommand();
            Game.CommandPublisher.PublishCommand(psrCommand);
            return false;
        }
        // Jump failed - create and publish a fall command
        var fallCommand = fallContextData.ToMechFallCommand();
        ProcessFallCommand(fallCommand, mech);
        return true;
    }
    
    private void ProcessFallCommand(MechFallCommand fallCommand, Mech mech, bool publishCommand = true)
    {
        var action = new ApplyFallAction(mech, fallCommand);
        var commands = action.Process(Game);
        if (publishCommand)
        {
            foreach (var cmd in commands)
                Game.CommandPublisher.PublishCommand(cmd);
        }
        else
        {
            // Publish everything EXCEPT the fallCommand itself
            foreach (var cmd in commands)
            {
                if (!cmd.Equals(fallCommand))
                    Game.CommandPublisher.PublishCommand(cmd);
            }
        }
    }

    public override PhaseNames Name => PhaseNames.Movement;

    private void ClearMovementDeferralState()
    {
        _deferredMovementUnitId = null;
        _requestDeferStepConsumption = false;
    }
}
