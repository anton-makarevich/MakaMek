using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Resolves one limb-based physical attack and publishes its resolution command.
/// Extracted from <see cref="PhysicalAttackResolutionPhase"/>.
/// </summary>
internal sealed class PhysicalAttackSingleResolver
{
    private readonly ServerGame _game;
    private readonly PhysicalAttackConsequenceApplier _consequences;

    public PhysicalAttackSingleResolver(ServerGame game, PhysicalAttackConsequenceApplier consequences)
    {
        _game = game;
        _consequences = consequences;
    }

    public PhysicalAttackResolutionData Resolve(AttackQueueItem item)
    {
        var target = item.Context.Target;
        var attacker = item.Context.Attacker;
        var resolution = _game.PhysicalAttackResolver.ResolveAttack(item.Context);

        var targetDestroyedPartsBefore = PhysicalAttackResolutionMetadata.GetDestroyedParts(target);
        var targetWasDestroyedBefore = target.IsDestroyed;
        var attackerDestroyedPartsBefore = PhysicalAttackResolutionMetadata.GetDestroyedParts(attacker);
        var attackerWasDestroyedBefore = attacker.IsDestroyed;

        var command = new PhysicalAttackResolutionCommand
        {
            ResolutionId = Guid.NewGuid(),
            GameOriginId = _game.Id,
            PlayerId = item.Player.Id,
            AttackerId = attacker.Id,
            TargetId = target.Id,
            ResolutionData = resolution
        };
        // The server transport does not loop commands back to the server game.
        // Apply the authoritative result before publishing it to clients.
        _game.OnPhysicalAttackResolution(command);

        resolution = PhysicalAttackResolutionMetadata.AttachDestructionMetadata(
            resolution,
            target, targetDestroyedPartsBefore, targetWasDestroyedBefore,
            attacker, attackerDestroyedPartsBefore, attackerWasDestroyedBefore);
        _game.CommandPublisher.PublishCommand(command with { ResolutionData = resolution });

        _consequences.ApplyDamageConsequences(target, resolution.AttackResolution);
        _consequences.ApplyPsrRequirements(attacker, target, resolution.PsrRequirements);

        return resolution;
    }
}
