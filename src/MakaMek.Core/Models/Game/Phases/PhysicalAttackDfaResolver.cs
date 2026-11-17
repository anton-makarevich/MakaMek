using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Resolves a Death From Above declared in the movement phase: target selection, no-roll miss
/// paths, the to-hit roll, and publishing the resolution with its damage and PSR consequences.
/// Extracted from <see cref="PhysicalAttackResolutionPhase"/>.
/// </summary>
internal sealed class PhysicalAttackDfaResolver
{
    private readonly ServerGame _game;
    private readonly DfaDisplacementResolver _dfaDisplacementResolver;
    private readonly IDfaAttackResolver _dfaAttackResolver;
    private readonly PhysicalAttackDisplacementService _displacement;
    private readonly PhysicalAttackConsequenceApplier _consequences;
    private readonly PhysicalAttackDfaLandingResolver _landing;
    private readonly PhysicalAttackDfaDisplacementResolver _dfaDisplacement;
    private readonly PhysicalAttackDfaResolutionPublisher _publisher;

    public PhysicalAttackDfaResolver(
        ServerGame game,
        PhysicalAttackPhaseState state,
        DfaDisplacementResolver dfaDisplacementResolver,
        IDfaAttackResolver dfaAttackResolver,
        PhysicalAttackDisplacementService displacement,
        PhysicalAttackConsequenceApplier consequences)
    {
        _game = game;
        _dfaDisplacementResolver = dfaDisplacementResolver;
        _dfaAttackResolver = dfaAttackResolver;
        _displacement = displacement;
        _consequences = consequences;
        _landing = new PhysicalAttackDfaLandingResolver(game, state, dfaDisplacementResolver, displacement);
        _dfaDisplacement = new PhysicalAttackDfaDisplacementResolver(displacement, _landing);
        _publisher = new PhysicalAttackDfaResolutionPublisher(game, consequences);
    }

    /// <summary>
    /// Resolves a Death From Above: the resolution state the declaration validator reports, the
    /// target it selects, the no-roll miss paths, the to-hit roll and then either the hit
    /// consequences or the miss's automatic target displacement.
    /// </summary>
    public void Resolve(DfaQueueItem item)
    {
        if (_game.BattleMap is not { } map) return;
        if (item.Attacker.IsDestroyed) return;

        var declaredTarget = _displacement.AllUnits().FirstOrDefault(u => u.Id == item.Declaration.TargetId);
        var attackerState = _game.MovementAttackValidator
            .GetAttackerState(item.Attacker, item.Declaration);
        if (attackerState == MovementAttackAttackerState.CannotAttack)
        {
            ResolveAutoFail(item, declaredTarget);
            return;
        }

        // An earlier attack of the phase can displace, destroy or fell the declared target. The
        // validator's target rule then names the enemy that replaced it, which is attacked at +1,
        // or reports an automatic failure. A 'Mech making its own Death From Above is immune to
        // physical attacks until it lands (p. 149), so the rule never selects an airborne performer.
        var selection = _game.MovementAttackValidator
            .SelectResolutionTarget(item.Attacker, item.Declaration, _displacement.AllUnits(), map);
        if (selection.Target is not { } target)
        {
            // An attacker that must miss still crashes even when nobody holds the committed hex:
            // the attack misses first, and the missing target only removes the target side (p. 149).
            ResolveAutoFail(item, declaredTarget,
                attackerState == MovementAttackAttackerState.MustMiss);
            return;
        }

        // A target destroyed in place does not move and does not resolve a displacement; the
        // attacker falls into its hex (Total Warfare, p. 150). A prone, shutdown or unconscious
        // attacker also resolves as a miss without a to-hit roll; a fall does not cancel the
        // pending DFA (docs/rules/physical-attacks.md section 3.1).
        if (target.IsDestroyed || attackerState == MovementAttackAttackerState.MustMiss)
        {
            ResolveMiss(item, target,
                PhysicalAttackDfaResolutionPublisher.CreateAutoResolution(item, DfaOutcome.Miss, selection.IsReplacementTarget));
            return;
        }

        var resolution = _dfaAttackResolver.ResolveDfa(new DfaAttackContext
        {
            Attacker = item.Attacker,
            Target = target,
            Map = map,
            StartCoordinates = item.StartCoordinates,
            ApproachCoordinates = item.ApproachCoordinates,
            IsReplacementTarget = selection.IsReplacementTarget,
            OriginalTargetId = item.Declaration.TargetId
        });

        if (resolution.AttackResolution.IsHit)
        {
            var displacement = _dfaDisplacementResolver.ResolveHit(
                item.Attacker,
                target,
                item.StartCoordinates,
                map,
                _displacement.AllUnits(),
                _game.Id);

            // A target that cannot be displaced is destroyed so the attacker can take its hex
            // (Total Warfare, p. 150). A target already destroyed before the impact does not need
            // this and does not move.
            var destroyTarget = displacement is { TargetDestination: null }
                                && !target.IsDestroyed;
            _ = _publisher.Publish(
                item,
                target,
                resolution,
                destroyTarget);
            _dfaDisplacement.ApplyHit(item, target, displacement);
            _landing.ApplyWaterEntryModifier(item.Attacker, map);
        }
        else
        {
            // A miss is published by ResolveMiss once the zero-candidate outcome is known.
            ResolveMiss(item, target, resolution);
        }
    }

    /// <summary>
    /// Resolves a DFA miss: the target is displaced into the hex the resolver picks along the jump
    /// vector, the attacker lands in the target's original hex and automatically falls two levels.
    /// No adjacent hex available destroys the attacker (Total Warfare, p. 150).
    /// </summary>
    private void ResolveMiss(
        DfaQueueItem item,
        IUnit target,
        PhysicalAttackResolutionData resolution)
    {
        var map = _game.BattleMap;
        if (map is null) return;

        // A target destroyed in place does not move; the attacker still falls into its hex.
        if (target.IsDestroyed)
        {
            _publisher.Publish(item, target, resolution);
            if (target.Position is not null)
            {
                var landing = _landing.Execute(item.Attacker);
                _displacement.RollDominoEffects(landing?.DominoEffect);
                _landing.ApplyFall(item.Attacker);
            }
            return;
        }

        if (target.Position is null) return;

        var candidate = _dfaDisplacementResolver.ResolveMiss(
            target,
            item.StartCoordinates,
            map,
            _displacement.AllUnits());

        if (candidate is null)
        {
            // No legal adjacent hex exists: the attacker is destroyed (Total Warfare, p. 150).
            if (resolution.Dfa is not { } dfa)
                throw new InvalidOperationException("A DFA miss resolution must carry DFA data.");
            var destroyed = resolution with
            {
                Dfa = dfa with { Outcome = DfaOutcome.AttackerDestroyedNoLanding }
            };
            _publisher.Publish(item, target, destroyed);
            return;
        }

        _publisher.Publish(item, target, resolution);

        _dfaDisplacement.ApplyMiss(
            item.Attacker,
            target,
            candidate,
            DisplacementReason.DfaTarget);
    }

    /// <summary>
    /// Publishes an attack that could not resolve (the target left the declared hex, or the attacker
    /// is no longer at its approach hex) and grounds the attacker in the hex it committed to land in.
    /// The jump destination of a DFA is the target hex, so a failed attack never ends anywhere else;
    /// an occupied hex is resolved through the displacement pipeline. If no legal landing remains,
    /// the attacker is destroyed. No damage and no fall (Total Warfare, p. 150) — unless the attacker
    /// had to miss anyway (prone, shutdown or unconscious), because then the miss still carries its
    /// automatic two-level fall even though the target left the hex (p. 149-150).
    /// </summary>
    private void ResolveAutoFail(DfaQueueItem item, IUnit? target, bool applyMissFall = false)
    {
        var landing = _landing.Resolve(item.Attacker);
        var noLanding = landing is null || landing.IsOverlapUnresolved;
        _publisher.Publish(
            item,
            target,
            PhysicalAttackDfaResolutionPublisher.CreateAutoResolution(
                item,
                noLanding ? DfaOutcome.AttackerDestroyedNoLanding
                    : applyMissFall ? DfaOutcome.Miss : DfaOutcome.AutoFail));

        if (noLanding) return;

        var appliedLanding = _landing.Apply(
            item.Attacker, landing,
            applyMissFall ? DfaLandingKind.MissLanding : DfaLandingKind.AutoFailLanding);
        _displacement.RollDominoEffects(appliedLanding?.DominoEffect);

        // A landing that could not be resolved has already destroyed the attacker; it cannot fall.
        if (applyMissFall && !item.Attacker.IsDestroyed) _landing.ApplyFall(item.Attacker);
    }

}
