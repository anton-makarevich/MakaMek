using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Resolves a declared charge: target selection (or explicit auto-fail), resolution command,
/// damage consequences and the post-charge displacement. Extracted from
/// <see cref="PhysicalAttackResolutionPhase"/>.
/// </summary>
internal sealed class PhysicalAttackChargeResolver
{
    private readonly ServerGame _game;
    private readonly PhysicalAttackDisplacementService _displacement;
    private readonly PhysicalAttackChargeDisplacement _chargeDisplacement;
    private readonly PhysicalAttackConsequenceApplier _consequences;

    public PhysicalAttackChargeResolver(
        ServerGame game,
        ChargeDisplacementResolver chargeDisplacementResolver,
        PhysicalAttackDisplacementService displacement,
        PhysicalAttackConsequenceApplier consequences)
    {
        _game = game;
        _displacement = displacement;
        _consequences = consequences;
        _chargeDisplacement = new PhysicalAttackChargeDisplacement(game, chargeDisplacementResolver, displacement);
    }

    public void Resolve(ChargeQueueItem item)
    {
        var targetSelection = item.AttackerAutoFail
            ? ChargeTargetSelection.AutoFail()
            : ChargeTargetSelector.Select(item.Attacker, item.DeclaredTargetId, _displacement.AllUnits());

        if (targetSelection.Target is not { } target)
        {
            ResolveAutoFail(item);
            return;
        }

        var attacker = item.Attacker;
        var resolution = _game.PhysicalAttackResolver.ResolveCharge(new ChargeAttackContext
        {
            Attacker = attacker,
            Target = target,
            Map = _game.BattleMap ?? throw new InvalidOperationException("Battle map is null"),
            HexesMoved = item.HexesMoved,
            IsReplacementTarget = targetSelection.IsReplacementTarget,
            OriginalTargetId = item.DeclaredTargetId
        });

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
        _game.OnPhysicalAttackResolution(command);

        resolution = PhysicalAttackResolutionMetadata.AttachDestructionMetadata(
            resolution,
            target, targetDestroyedPartsBefore, targetWasDestroyedBefore,
            attacker, attackerDestroyedPartsBefore, attackerWasDestroyedBefore);
        _game.CommandPublisher.PublishCommand(command with { ResolutionData = resolution });

        _consequences.ApplyDamageConsequences(target, resolution.AttackResolution);
        if (resolution.AttackResolution.IsHit)
            _consequences.ApplyDamageConsequences(
                attacker,
                resolution.Charge?.AttackerCollision,
                resolution.Charge?.AttackerDestroyedParts);

        _consequences.ApplyPsrRequirements(attacker, target, resolution.PsrRequirements);

        switch (resolution.Charge?.Outcome)
        {
            case ChargeOutcome.Hit:
                _chargeDisplacement.ApplyHit(attacker, target);
                break;
            case ChargeOutcome.Miss:
                _chargeDisplacement.ApplyMiss(attacker);
                break;
        }
    }

    /// <summary>
    /// Publishes the automatic failure of a charge. No roll is made and no damage or piloting
    /// skill roll is created. When the failure is on the target side, the attacker is placed
    /// exactly like a rolled miss: in a side hex of its forward arc, never in the destination hex
    /// (Total Warfare, p. 148). An attacker-side failure leaves the attacker where it is.
    /// </summary>
    private void ResolveAutoFail(ChargeQueueItem item)
    {
        var command = PhysicalAttackResolutionFactory.AutoFailCharge(_game.Id, item);
        _game.OnPhysicalAttackResolution(command);
        _game.CommandPublisher.PublishCommand(command);

        if (item.AttackerAutoFail) return;

        _chargeDisplacement.ApplyMiss(item.Attacker);
    }

}
