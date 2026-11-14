using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Applies the damage consequences of a physical attack (hull breach, critical hits,
/// consciousness) and accumulates the component hits / destroyed parts and PSR contexts that the
/// end-of-phase rolls need. Extracted from <see cref="PhysicalAttackResolutionPhase"/>.
/// </summary>
internal sealed class PhysicalAttackConsequenceApplier
{
    private readonly ServerGame _game;
    private readonly PhysicalAttackPhaseState _state;

    public PhysicalAttackConsequenceApplier(ServerGame game, PhysicalAttackPhaseState state)
    {
        _game = game;
        _state = state;
    }

    /// <summary>
    /// Applies the hull breach, critical hits and consciousness consequences of a unit's damage
    /// and accumulates the component hits and destroyed parts for the end-of-phase rolls. A charge
    /// calls it for the target and, on a hit, for the attacker as well.
    /// </summary>
    public void ApplyDamageConsequences(IUnit unit, AttackResolutionData resolution) =>
        ApplyDamageConsequences(unit, resolution.HitLocationsData, resolution.DestroyedParts);

    public void ApplyDamageConsequences(
        IUnit unit,
        AttackHitLocationsData? hitLocationsData,
        IReadOnlyList<PartLocation>? destroyedParts = null)
    {
        var damage = hitLocationsData?.HitLocations
            .SelectMany(h => h.Damage).ToList() ?? [];
        if (damage.Any(d => d.ArmorDamage > 0 || d.StructureDamage > 0))
        {
            var hullBreachCommand = _game.HullBreachCalculator.CalculateAndApplyHullBreach(unit, damage);
            if (hullBreachCommand != null)
            {
                hullBreachCommand.GameOriginId = _game.Id;
                _game.CommandPublisher.PublishCommand(hullBreachCommand);
            }
        }

        List<ComponentHitData> componentHits = [];
        List<PartLocation> blownOffParts = [];
        if (damage.Any(d => d.StructureDamage > 0))
        {
            var criticalHitsCommand = _game.CriticalHitsCalculator.CalculateCriticalHits(unit, damage);
            if (criticalHitsCommand != null)
            {
                criticalHitsCommand.GameOriginId = _game.Id;
                _game.OnCriticalHitsResolution(criticalHitsCommand);
                _game.CommandPublisher.PublishCommand(criticalHitsCommand);

                componentHits = criticalHitsCommand.CriticalHits.SelectMany(ch => ch.HitComponents ?? []).ToList();
                blownOffParts = criticalHitsCommand.CriticalHits
                    .Where(ch => ch.IsBlownOff).Select(ch => ch.Location).ToList();
            }
        }

        // After the resolution and critical hits commands, to keep the game log in order
        ConsciousnessRolls.Process(_game, unit);

        var accumulatedDamage = _state.GetAccumulatedDamage(unit);
        accumulatedDamage.ComponentHits.AddRange(componentHits);
        foreach (var part in (destroyedParts ?? []).Concat(blownOffParts))
            accumulatedDamage.DestroyedParts.Add(part);
    }

    public void ApplyPsrRequirements(
        IUnit attacker,
        IUnit target,
        IReadOnlyList<PhysicalAttackPsrRequirement> requirements)
    {
        foreach (var requirement in requirements)
        {
            var recipient = requirement.Recipient switch
            {
                PhysicalAttackParticipant.Target => target,
                PhysicalAttackParticipant.Attacker => attacker,
                _ => throw new NotSupportedException(
                    $"No piloting skill roll recipient is defined for {requirement.Recipient}")
            };
            var contexts = _state.GetAccumulatedDamage(recipient).RollContexts;
            var context = GetRollContext(requirement.RollType);
            contexts.Add(context);

            // The push PSR is written before the displacement is applied. Keep a handle on it so
            // the water-entry modifier can be added once the destination is known.
            if (context is PushHitRollContext pushHitContext)
                _state.PushHitContexts[recipient.Id] = (contexts, pushHitContext);
        }
    }

    private static PilotingSkillRollContext GetRollContext(PilotingSkillRollType rollType) => rollType switch
    {
        PilotingSkillRollType.KickHit => new KickHitRollContext(),
        PilotingSkillRollType.KickMiss => new KickMissRollContext(),
        PilotingSkillRollType.PushHit => new PushHitRollContext(),
        PilotingSkillRollType.ChargeImpact => new ChargeImpactRollContext(),
        _ => throw new NotSupportedException($"No piloting skill roll context is defined for {rollType}")
    };
}
