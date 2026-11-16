using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Publishes DFA resolutions with their damage, destruction metadata and piloting skill rolls.
/// Extracted from <see cref="PhysicalAttackDfaResolver"/>.
/// </summary>
internal sealed class PhysicalAttackDfaResolutionPublisher
{
    private readonly ServerGame _game;
    private readonly PhysicalAttackConsequenceApplier _consequences;

    public PhysicalAttackDfaResolutionPublisher(
        ServerGame game,
        PhysicalAttackConsequenceApplier consequences)
    {
        _game = game;
        _consequences = consequences;
    }

    public static PhysicalAttackResolutionData CreateAutoResolution(
        DfaQueueItem item,
        DfaOutcome outcome,
        bool isReplacementTarget = false) =>
        new(
            PhysicalAttackType.DFA,
            null,
            0,
            new AttackResolutionData(0, [], false, HitDirection.Front, 0),
            [],
            Dfa: new DfaResolutionData(
                outcome,
                OriginalTargetId: item.Declaration.TargetId,
                IsReplacementTarget: isReplacementTarget));

    /// <summary>
    /// Applies the DFA's damage, destruction metadata and piloting skill rolls, publishing the
    /// resolution in the weapon-phase command order.
    /// </summary>
    public PhysicalAttackResolutionData Publish(
        DfaQueueItem item,
        IUnit? target,
        PhysicalAttackResolutionData resolution,
        bool destroyTarget = false)
    {
        var targetDestroyedPartsBefore = target is null ? [] : target.GetDestroyedParts();
        var targetWasDestroyedBefore = target?.IsDestroyed == true;
        var attackerDestroyedPartsBefore = item.Attacker.GetDestroyedParts();
        var attackerWasDestroyedBefore = item.Attacker.IsDestroyed;

        var command = new PhysicalAttackResolutionCommand
        {
            ResolutionId = Guid.NewGuid(),
            GameOriginId = _game.Id,
            PlayerId = item.Player.Id,
            AttackerId = item.Attacker.Id,
            TargetId = target?.Id ?? item.Declaration.TargetId,
            ResolutionData = resolution
        };

        // The server transport does not loop commands back to the server game.
        _game.OnPhysicalAttackResolution(command);

        // Damage is applied before displacement. If the blocked destination destroys the target,
        // mark it now so the published metadata and every client agree that the target is gone.
        // The target is only destroyed to vacate the hex for the attacker: when the impact
        // destroyed the attacker it never lands, so the target keeps its post-impact state
        // (Total Warfare, p. 150).
        if (destroyTarget && target is { IsDestroyed: false } && !item.Attacker.IsDestroyed)
            target.MarkDestroyed();

        resolution = resolution.AttachDestructionMetadata(
            target, targetDestroyedPartsBefore, targetWasDestroyedBefore,
            item.Attacker, attackerDestroyedPartsBefore, attackerWasDestroyedBefore);

        _game.CommandPublisher.PublishCommand(command with { ResolutionData = resolution });

        if (target is not null)
        {
            _consequences.ApplyDamageConsequences(target, resolution.AttackResolution);
            _consequences.ApplyPsrRequirements(item.Attacker, target, resolution.PsrRequirements);
        }

        if (resolution.Dfa?.AttackerLegHitLocations is { } attackerLegs)
            _consequences.ApplyDamageConsequences(
                item.Attacker,
                attackerLegs,
                resolution.Dfa.AttackerDestroyedParts);

        return resolution;
    }
}
