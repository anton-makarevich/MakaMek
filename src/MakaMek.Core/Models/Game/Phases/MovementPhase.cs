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
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public class MovementPhase(ServerGame game) : MainGamePhase(game)
{
    public override void HandleCommand(IGameCommand command)
    {
        switch (command)
        {
            case MoveUnitCommand moveCommand:
                HandleUnitAction(command, moveCommand.PlayerId);
                break;
            case TryStandupCommand standupCommand:
                ProcessStandupCommand(standupCommand);  //HandleUnitAction moves to the new unit, should not happen here
                break;
        }
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
                    unit, new EnteringDeepWaterRollContext(entry.WaterDepth), Game);
                    
                if (fallContextData.IsFalling)
                {
                    var truncatedSegments = moveCommand.MovementPath.Take(entry.SegmentIndex + 1).ToList();
                    var truncatedPath = new MovementPath(truncatedSegments, moveCommand.MovementType);
                    var truncatedCommand = moveCommand with { MovementPath = truncatedPath.ToData() };
                    
                    Game.OnMoveUnit(truncatedCommand);
                    var broadcastCommand = truncatedCommand with { GameOriginId = Game.Id };
                    Game.CommandPublisher.PublishCommand(broadcastCommand);
                    
                    var fallCommand = fallContextData.ToMechFallCommand();
                    ProcessFallCommand(fallCommand, unit);
                    return;
                }
                
                var psrCommand = fallContextData.ToMechFallCommand();
                Game.CommandPublisher.PublishCommand(psrCommand);
            }
        }

        Game.OnMoveUnit(moveCommand);
        var fullBroadcastCommand = moveCommand with { GameOriginId = Game.Id };
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
                        unit, new EnteringDeepWaterRollContext(-1*water.Height), Game);
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
            // TODO: Should return error command
            Game.Logger.LogWarning("Unit not found");
            return;
        }
        
        var broadcastCommand = tryStandUpCommand with { GameOriginId = Game.Id };
        Game.CommandPublisher.PublishCommand(broadcastCommand);

        // Check if the unit can stand up (has sufficient MP, pilot is conscious, etc.)
        if (!unit.CanStandup() || unit.Position == null)
        {
            return; // Cannot stand up
        }

        // Use the FallProcessor to process the standup attempt and get context data
        var fallContextData = Game.FallProcessor.ProcessMovementAttempt(
            unit, new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt), Game);
        
        // Create and publish the appropriate command based on the result
        if (fallContextData.IsFalling)
        {
            // Standup failed - create and publish a fall command
            var fallCommand = fallContextData.ToMechFallCommand();
            ProcessFallCommand(fallCommand, unit);
        }
        else
        {
            // Standup succeeded - create and publish a standup command
            var standUpCommand = fallContextData.ToMechStandUpCommand(tryStandUpCommand.NewFacing);
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
            mech, new PilotingSkillRollContext(PilotingSkillRollType.JumpWithDamage), Game);
        
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
    
    private void ProcessFallCommand(MechFallCommand fallCommand, Mech mech)
    {
        Game.OnMechFalling(fallCommand);
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
}
