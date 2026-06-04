using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
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

    private List<(int SegmentIndex, int WaterDepth)> FindWaterEntrySegments(IReadOnlyList<PathSegmentData> movementPath)
    {
        var entries = new List<(int SegmentIndex, int WaterDepth)>();
        for (var i = 0; i < movementPath.Count; i++)
        {
            var segment = movementPath[i];
            if (segment.From.Coordinates == segment.To.Coordinates) continue;
            
            var destinationHex = Game.BattleMap?.GetHex(new HexCoordinates(segment.To.Coordinates));
            if (destinationHex?.GetTerrain(MakaMekTerrains.Water) is WaterTerrain { Height : <= -1 } water)
            {
                entries.Add((i, -1*water.Height));
            }
        }
        return entries;
    }

    private List<(int SegmentIndex, int HexesMoved)> FindSkidTriggerSegments(
        IReadOnlyList<PathSegmentData> movementPath,
        MovementType movementType)
    {
        var triggers = new List<(int SegmentIndex, int HexesMoved)>();

        if (movementType != MovementType.Run)
            return triggers;

        for (var i = 0; i < movementPath.Count; i++)
        {
            var segment = movementPath[i];

            if (segment.From.Coordinates != segment.To.Coordinates)
                continue;

            var turnHex = Game.BattleMap?.GetHex(new HexCoordinates(segment.From.Coordinates));
            if (turnHex == null) continue;
            if (!turnHex.HasHardPavement())
                continue;

            if (segment.To.Coordinates == movementPath.Last().To.Coordinates)
                continue;

            var hexesMoved = 0;
            for (var j = 0; j < i; j++)
            {
                var prevSegment = movementPath[j];
                if (prevSegment.From.Coordinates != prevSegment.To.Coordinates)
                    hexesMoved++;
            }

            triggers.Add((i, hexesMoved));
        }

        return triggers;
    }

    private void ProcessMoveCommand(MoveUnitCommand moveCommand)
    {
        var player = Game.Players.FirstOrDefault(p => p.Id == moveCommand.PlayerId);
        // Find the unit
        var unit = player?.Units.FirstOrDefault(u => u.Id == moveCommand.UnitId) as Mech;

        if (unit != null && moveCommand.MovementType != MovementType.Jump)
        {
            var waterEntries = FindWaterEntrySegments(moveCommand.MovementPath);
            foreach (var entry in waterEntries)
            {
                var fallContextData = Game.FallProcessor.ProcessMovementAttempt(
                    unit, new EnteringDeepWaterRollContext(entry.WaterDepth), Game, moveCommand.MovementType);
                    
                if (fallContextData.IsFalling)
                {
                    var truncatedSegments = moveCommand.MovementPath.Take(entry.SegmentIndex + 1).ToList();
                    var truncatedPath = new MovementPath(truncatedSegments, moveCommand.MovementType);
                    truncatedPath = truncatedPath.WithLastSegmentEvent(new SegmentEvent(SegmentEventType.Fall));
                    var truncatedCommand = moveCommand with
                    {
                        MovementPath = truncatedPath.ToData(),
                        IsCompleted = false
                    };
                    
                    Game.OnMoveUnit(truncatedCommand);
                    
                    var fallCommand = fallContextData.ToMechFallCommand();
                    ProcessFallCommand(fallCommand, unit,false);
                    
                    var canStandup = unit.CanStandup();
                    
                    var broadcastCommand = truncatedCommand with
                    {
                        GameOriginId = Game.Id,
                        IsCompleted = !canStandup
                    };
                    Game.CommandPublisher.PublishCommand(broadcastCommand);
                    Game.CommandPublisher.PublishCommand(fallCommand);
                    
                    if (!canStandup && unit.Position != null)
                    {
                        var completionCommand = truncatedCommand with
                        {
                            IsCompleted = true,
                            MovementPath = MovementPath.CreateSingleSegmentPath(unit.Position, truncatedCommand.MovementType).ToData()
                        };
                        // this is only to complete movement on the server; on client it already handled by the prev command
                        Game.OnMoveUnit(completionCommand);
                    }
                    _requestDeferStepConsumption = canStandup;
                    return;
                }
                
                var psrCommand = fallContextData.ToMechFallCommand();
                Game.CommandPublisher.PublishCommand(psrCommand);
            }

            var skidTriggers = FindSkidTriggerSegments(moveCommand.MovementPath, moveCommand.MovementType);
            foreach (var (triggerSegmentIndex, hexesMoved) in skidTriggers)
            {
                var triggerSegment = moveCommand.MovementPath[triggerSegmentIndex];
                var skidContext = new SkidCheckRollContext(hexesMoved);
                var skidFallContext = Game.FallProcessor.ProcessMovementAttempt(
                    unit, skidContext, Game, moveCommand.MovementType);

                if (skidFallContext.IsFalling)
                {
                    var truncatedSegments = moveCommand.MovementPath.Take(triggerSegmentIndex + 1).ToList();
                    var truncatedPath = new MovementPath(truncatedSegments, moveCommand.MovementType);
                    truncatedPath = truncatedPath.WithLastSegmentEvent(new SegmentEvent(SegmentEventType.Skid));

                    var skidDistance = (int)Math.Ceiling(hexesMoved / 2.0);
                    var turnHexCoords = new HexCoordinates(triggerSegment.From.Coordinates);
                    var skidFacing = (HexDirection)triggerSegment.From.Facing;

                    var skidPathSegments = new List<PathSegment>();
                    var currentCoords = turnHexCoords;
                    var currentHex = Game.BattleMap!.GetHex(currentCoords)!;
                    var remainingSkidDistance = skidDistance;

                    while (remainingSkidDistance > 0)
                    {
                        var nextCoords = currentCoords.GetNeighbour(skidFacing);
                        var nextHex = Game.BattleMap?.GetHex(nextCoords);
                        if (nextHex == null)
                            break;

                        var movementCost = nextHex.GetEnterMovementCost(currentHex);
                        var fromPos = new HexPosition(currentCoords, skidFacing);
                        var toPos = new HexPosition(nextCoords, skidFacing);
                        var skidSegment = new PathSegment(fromPos, toPos, [])
                        {
                            Events = [new SegmentEvent(SegmentEventType.Skid)]
                        };
                        skidPathSegments.Add(skidSegment);

                        remainingSkidDistance -= movementCost.Value;
                        currentCoords = nextCoords;
                        currentHex = nextHex;
                    }

                    var allSegments = truncatedPath.Segments
                        .Select(s => s.ToData())
                        .Concat(skidPathSegments.Select(s => s.ToData()))
                        .ToList();

                    var modifiedPath = new MovementPath(allSegments, moveCommand.MovementType);
                    var modifiedCommand = moveCommand with
                    {
                        MovementPath = modifiedPath.ToData(),
                        IsCompleted = true
                    };

                    Game.OnMoveUnit(modifiedCommand);

                    var fallCommand = skidFallContext.ToMechFallCommand();
                    ProcessFallCommand(fallCommand, unit);

                    Game.CommandPublisher.PublishCommand(modifiedCommand with { GameOriginId = Game.Id });
                    Game.CommandPublisher.PublishCommand(fallCommand);

                    return;
                }

                var skidPsrCommand = skidFallContext.ToMechFallCommand();
                Game.CommandPublisher.PublishCommand(skidPsrCommand);
            }
        }

        Game.OnMoveUnit(moveCommand);
        var fullBroadcastCommand = moveCommand with
        {
            GameOriginId = Game.Id,
            IsCompleted = true
        };
        Game.CommandPublisher.PublishCommand(fullBroadcastCommand);
        
        if (unit != null && moveCommand.MovementType == MovementType.Jump)
        {
            var fell = false;
            if (unit.IsPsrForJumpRequired())
            {
                fell = ProcessJumpWithDamage(unit);
            }
            
            if (!fell && moveCommand.MovementPath.Count > 0)
            {
                var lastSegment = moveCommand.MovementPath.Last();
                var destHex = Game.BattleMap?.GetHex(new HexCoordinates(lastSegment.To.Coordinates));
                if (destHex?.GetTerrain(MakaMekTerrains.Water) is WaterTerrain { Height: <= -1 } water)
                {
                    var fallContextData = Game.FallProcessor.ProcessMovementAttempt(
                        unit, new EnteringDeepWaterRollContext(-1*water.Height), Game, MovementType.Jump);
                    if (fallContextData.IsFalling)
                    {
                        var fallCommand = fallContextData.ToMechFallCommand();
                        ProcessFallCommand(fallCommand, unit);
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
            // Standup failed - add StandupAttempt event, then fall
            unit.MovementTaken ??= MovementPath.CreateSingleSegmentPath(unit.Position!, MovementType.StandingStill);
            unit.MovementTaken = unit.MovementTaken.WithLastSegmentEvent(
                new SegmentEvent(SegmentEventType.StandupAttempt),
                new StandUpAttemptMovementCost { Value = 2 });
            var fallCommand = fallContextData.ToMechFallCommand();
            ProcessFallCommand(fallCommand, unit);
        }
        else
        {
            // Standup succeeded - add StandupAttempt event, then stand up
            unit.MovementTaken ??= MovementPath.CreateSingleSegmentPath(unit.Position!, movementTypeAfterStandup);
            unit.MovementTaken = unit.MovementTaken.WithLastSegmentEvent(
                new SegmentEvent(SegmentEventType.StandupAttempt),
                new StandUpAttemptMovementCost { Value = 2 });
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
        mech.MovementTaken = mech.MovementTaken?.WithLastSegmentEvent(new SegmentEvent(SegmentEventType.Fall));
        Game.OnMechFalling(fallCommand);
        if (publishCommand)
            Game.CommandPublisher.PublishCommand(fallCommand);

        var locationsWithDamagedStructure = fallCommand.DamageData?.HitLocations.HitLocations
            .Where(h => h.Damage.Any(d => d.StructureDamage > 0))
            .SelectMany(h => h.Damage)
            .ToList()??[];
        if (locationsWithDamagedStructure.Count != 0)
        {
            var fallCriticalHitsCommand = Game.CriticalHitsCalculator
                .CalculateAndApplyCriticalHits(mech, locationsWithDamagedStructure);
            if (fallCriticalHitsCommand != null)
            {
                fallCriticalHitsCommand.GameOriginId = Game.Id;
                Game.CommandPublisher.PublishCommand(fallCriticalHitsCommand);
            }
        }
        // Process consciousness rolls for pilot damage accumulated during this phase
        ProcessConsciousnessRollsForUnit(mech);
    }

    public override PhaseNames Name => PhaseNames.Movement;

    private void ClearMovementDeferralState()
    {
        _deferredMovementUnitId = null;
        _requestDeferStepConsumption = false;
    }
}
