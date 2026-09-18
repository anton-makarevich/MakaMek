using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public class PhysicalAttackPhase(ServerGame game) : MainGamePhase(game)
{
    private readonly PhysicalAttackValidator _validator = new();

    public override void HandleCommand(IGameCommand command)
    {
        var playerId = command switch
        {
            PhysicalAttackCommand attackCommand => attackCommand.PlayerId,
            PassPhysicalAttackCommand passCommand => passCommand.PlayerId,
            _ => Guid.Empty
        };

        if (playerId == Guid.Empty) return;

        if (command is PhysicalAttackCommand physicalAttackCommand && !IsValidAttack(physicalAttackCommand))
            return;

        HandleUnitAction(command, playerId);
    }

    private bool IsValidAttack(PhysicalAttackCommand command)
    {
        var attacker = Game.Players
            .SelectMany(player => player.Units)
            .FirstOrDefault(unit => unit.Id == command.UnitId);
        var target = Game.Players
            .SelectMany(player => player.Units)
            .FirstOrDefault(unit => unit.Id == command.TargetUnitId);
        var result = _validator.Validate(attacker, target, command.AttackType);

        if (result.IsValid && command.AttackType == PhysicalAttackType.Push && target?.Position is { } targetPosition)
        {
            var attackerPosition = attacker!.Position!;
            var pushDirection = attackerPosition.Coordinates.GetDirectionToNeighbour(targetPosition.Coordinates);
            var destination = targetPosition.Coordinates.GetNeighbour(pushDirection);
            var destinationOccupied = Game.Players.SelectMany(player => player.Units)
                .Any(unit => unit.Id != target.Id && unit.Position?.Coordinates == destination);

            if ((Game.BattleMap is not null && Game.BattleMap.GetHex(destination) == null) || destinationOccupied)
                result = PhysicalAttackValidationResult.Invalid("Push destination must be on the map and unoccupied.");
        }

        if (!result.IsValid)
            Game.Logger.LogWarning("Rejected physical attack from {UnitId} to {TargetUnitId}: {Reason}",
                command.UnitId, command.TargetUnitId, result.Error);

        return result.IsValid;
    }

    protected override void ProcessCommand(IGameCommand command)
    {
        switch (command)
        {
            case PhysicalAttackCommand attackCommand:
                var attacker = Game.Players.SelectMany(player => player.Units)
                    .FirstOrDefault(unit => unit.Id == attackCommand.UnitId);
                var target = Game.Players.SelectMany(player => player.Units)
                    .FirstOrDefault(unit => unit.Id == attackCommand.TargetUnitId);
                if (attacker == null || target == null) break;

                // Acknowledge the accepted declaration before doing dice and damage work so
                // connected clients do not remain pending if result processing fails.
                var broadcastAttack = attackCommand;
                broadcastAttack.GameOriginId = Game.Id;
                Game.CommandPublisher.PublishCommand(broadcastAttack);

                var resolution = Game.PhysicalAttackResolver.Resolve(attacker, target, attackCommand.AttackType);
                if (resolution.IsHit && resolution.HitLocationsData is { } hitData)
                    target.ApplyDamage(hitData.HitLocations, resolution.AttackDirection);

                Game.CommandPublisher.PublishCommand(new PhysicalAttackResolutionCommand
                {
                    GameOriginId = Game.Id,
                    PlayerId = attackCommand.PlayerId,
                    AttackerId = attackCommand.UnitId,
                    TargetId = attackCommand.TargetUnitId,
                    AttackType = attackCommand.AttackType,
                    ResolutionData = resolution
                });

                if (attackCommand.AttackType == PhysicalAttackType.Push
                    && resolution.IsHit
                    && resolution.DisplacementTarget is { } displacementTarget
                    && target.Position is { } targetPosition)
                {
                    var displacement = new DisplaceUnitCommand
                    {
                        GameOriginId = Game.Id,
                        UnitId = target.Id,
                        FromCoordinates = new(targetPosition.Coordinates.Q, targetPosition.Coordinates.R),
                        ToCoordinates = displacementTarget,
                        NewFacing = (int)targetPosition.Facing,
                        DisplacementReason = DisplacementReason.PhysicalAttackPush
                    };
                    Game.OnUnitDisplaced(displacement);
                    Game.CommandPublisher.PublishCommand(displacement);
                }
                break;
            case PassPhysicalAttackCommand passCommand:
                var broadcastPass = passCommand;
                broadcastPass.GameOriginId = Game.Id;
                Game.CommandPublisher.PublishCommand(broadcastPass);
                break;
        }
    }

    public override PhaseNames Name => PhaseNames.PhysicalAttack;
}
