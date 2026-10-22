using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Resolves the physical attacks declared this turn, following the weapon attack resolution
/// pattern: a queue in initiative order with one item per declared limb, the pre-attack gates,
/// the resolver, then criticals, consciousness rolls and the end-of-phase piloting skill rolls.
/// </summary>
/// <remarks>
/// Physical attacks are simultaneous. Every gate runs while the queue is built, before any damage
/// of the phase, so an attack is never dropped because an earlier one in the same phase destroyed
/// or felled its attacker or its target. Damage is then applied attack by attack, as the weapon
/// phase does, because the resolver works out damage transfer from the target's current state: a
/// second hit on a location the first one destroyed has to move inward.
/// </remarks>
public class PhysicalAttackResolutionPhase(ServerGame game) : GamePhase(game)
{
    private readonly IReadOnlyList<IPhysicalAttackResolutionGate> _gates =
    [
        new AttackerDestroyedGate(),
        new AttackerProneGate(),
        new PhysicalAttackTargetValidityGate(game.PhysicalAttackEligibility)
    ];

    private readonly Dictionary<Guid, UnitPhaseAccumulatedDamage> _accumulatedDamage = new();

    private readonly record struct AttackQueueItem(IPlayer Player, PhysicalAttackContext Context);

    public override void Enter()
    {
        base.Enter();
        _accumulatedDamage.Clear();

        var queue = BuildAttackQueue();
        foreach (var item in queue)
            ResolveAttack(item);

        CalculateEndOfPhasePsrs();
        Game.TransitionToNextPhase(Name);
    }

    public override void HandleCommand(IGameCommand command)
    {
        // This phase doesn't process incoming commands, but we need to implement this method
    }

    public override PhaseNames Name => PhaseNames.PhysicalAttackResolution;

    /// <summary>
    /// One item per declared limb, in initiative order, keeping only the attacks every gate lets
    /// through. All the gates run here, before any damage of the phase.
    /// </summary>
    private List<AttackQueueItem> BuildAttackQueue()
    {
        var queue = new List<AttackQueueItem>();
        var declaringUnits = Game.InitiativeOrder
            .SelectMany(player => player.Units.Select(unit => (Player: player, Unit: unit)))
            .Where(entry => entry.Unit.DeclaredPhysicalAttack != null)
            .ToList();
        if (declaringUnits.Count == 0) return queue;

        var map = Game.BattleMap ?? throw new InvalidOperationException("Battle map is null");
        var allUnits = Game.Players.SelectMany(p => p.Units).ToList();

        foreach (var (player, attacker) in declaringUnits)
        {
            var declaration = attacker.DeclaredPhysicalAttack!;
            var target = allUnits.FirstOrDefault(u => u.Id == declaration.TargetId);
            if (target == null)
            {
                Game.Logger.LogWarning("Skipping {AttackType} by {AttackerName}: target {TargetId} is not in the game",
                    declaration.AttackType, attacker.Name, declaration.TargetId);
                continue;
            }

            foreach (var limb in declaration.AttackerLimbs)
            {
                var context = new PhysicalAttackContext
                {
                    Attacker = attacker,
                    Target = target,
                    AttackType = declaration.AttackType,
                    AttackerLimb = limb,
                    Map = map
                };
                if (_gates.Any(gate => gate.ShouldSkip(context, Game.Logger))) continue;

                queue.Add(new AttackQueueItem(player, context));
            }
        }

        return queue;
    }

    private void ResolveAttack(AttackQueueItem item)
    {
        var target = item.Context.Target;
        var resolution = Game.PhysicalAttackResolver.ResolveAttack(item.Context);

        var destroyedPartsBefore = target.Parts.Values
            .Where(p => p.IsDestroyed).Select(p => p.Location).ToList();
        var wasDestroyedBefore = target.IsDestroyed;

        var command = new PhysicalAttackResolutionCommand
        {
            ResolutionId = Guid.NewGuid(),
            GameOriginId = Game.Id,
            PlayerId = item.Player.Id,
            AttackerId = item.Context.Attacker.Id,
            TargetId = target.Id,
            ResolutionData = resolution
        };
        // The server transport does not loop commands back to the server game.
        // Apply the authoritative result before publishing it to clients.
        Game.OnPhysicalAttackResolution(command);

        var newlyDestroyedParts = target.Parts.Values
            .Where(p => p.IsDestroyed).Select(p => p.Location)
            .Except(destroyedPartsBefore).ToList();
        resolution = resolution with
        {
            AttackResolution = resolution.AttackResolution with
            {
                DestroyedParts = newlyDestroyedParts.Count != 0 ? newlyDestroyedParts : null,
                UnitDestroyed = !wasDestroyedBefore && target.IsDestroyed
            }
        };
        Game.CommandPublisher.PublishCommand(command with { ResolutionData = resolution });

        var damage = resolution.AttackResolution.HitLocationsData?.HitLocations
            .SelectMany(h => h.Damage).ToList() ?? [];
        if (damage.Any(d => d.ArmorDamage > 0 || d.StructureDamage > 0))
        {
            var hullBreachCommand = Game.HullBreachCalculator.CalculateAndApplyHullBreach(target, damage);
            if (hullBreachCommand != null)
            {
                hullBreachCommand.GameOriginId = Game.Id;
                Game.CommandPublisher.PublishCommand(hullBreachCommand);
            }
        }

        List<ComponentHitData> componentHits = [];
        List<PartLocation> blownOffParts = [];
        if (damage.Any(d => d.StructureDamage > 0))
        {
            var criticalHitsCommand = Game.CriticalHitsCalculator.CalculateCriticalHits(target, damage);
            if (criticalHitsCommand != null)
            {
                criticalHitsCommand.GameOriginId = Game.Id;
                Game.OnCriticalHitsResolution(criticalHitsCommand);
                Game.CommandPublisher.PublishCommand(criticalHitsCommand);

                componentHits = criticalHitsCommand.CriticalHits.SelectMany(ch => ch.HitComponents ?? []).ToList();
                blownOffParts = criticalHitsCommand.CriticalHits
                    .Where(ch => ch.IsBlownOff).Select(ch => ch.Location).ToList();
            }
        }

        // After the resolution and critical hits commands, to keep the game log in order
        ProcessConsciousnessRollsForUnit(target);

        var targetDamage = GetAccumulatedDamage(target);
        targetDamage.ComponentHits.AddRange(componentHits);
        foreach (var part in (resolution.AttackResolution.DestroyedParts ?? []).Concat(blownOffParts))
            targetDamage.DestroyedParts.Add(part);

        if (resolution.RequiresTargetPsr)
            targetDamage.RollContexts.Add(GetTargetRollContext(resolution.AttackType));
        if (resolution.RequiresAttackerPsr)
            GetAccumulatedDamage(item.Context.Attacker).RollContexts.Add(GetAttackerRollContext(resolution.AttackType));
    }

    private static PilotingSkillRollContext GetTargetRollContext(PhysicalAttackType attackType) => attackType switch
    {
        PhysicalAttackType.Kick => new KickHitRollContext(),
        _ => throw new NotSupportedException($"No target piloting skill roll is defined for a {attackType} attack")
    };

    private static PilotingSkillRollContext GetAttackerRollContext(PhysicalAttackType attackType) => attackType switch
    {
        PhysicalAttackType.Kick => new KickMissRollContext(),
        _ => throw new NotSupportedException($"No attacker piloting skill roll is defined for a {attackType} attack")
    };

    /// <summary>
    /// One unit at a time in initiative order: the rolls the phase's damage calls for first, then
    /// the rolls the attacks themselves call for. A unit that falls makes no further roll.
    /// </summary>
    private void CalculateEndOfPhasePsrs()
    {
        var units = Game.InitiativeOrder.SelectMany(p => p.Units);
        foreach (var unit in units)
        {
            if (!_accumulatedDamage.TryGetValue(unit.Id, out var accumulated)) continue;
            if (unit is not Mech { IsDestroyed: false } mech) continue;

            var fallCommands = Game.FallProcessor.ProcessPotentialFall(
                mech,
                Game,
                accumulated.ComponentHits,
                accumulated.DestroyedParts.ToList(),
                accumulated.RollContexts);

            foreach (var fallCommand in fallCommands)
            {
                Game.OnMechFalling(fallCommand);
                Game.CommandPublisher.PublishCommand(fallCommand);
                if (fallCommand.DamageData is null) continue;

                var fallStructureDamage = fallCommand.DamageData.HitLocations.HitLocations
                    .Where(h => h.Damage.Any(d => d.StructureDamage > 0))
                    .SelectMany(h => h.Damage)
                    .ToList();
                if (fallStructureDamage.Count != 0)
                {
                    var fallCriticalHitsCommand = Game.CriticalHitsCalculator
                        .CalculateCriticalHits(mech, fallStructureDamage);
                    if (fallCriticalHitsCommand != null)
                    {
                        fallCriticalHitsCommand.GameOriginId = Game.Id;
                        Game.OnCriticalHitsResolution(fallCriticalHitsCommand);
                        Game.CommandPublisher.PublishCommand(fallCriticalHitsCommand);
                    }
                }

                ProcessConsciousnessRollsForUnit(mech);
                break;
            }
        }

        _accumulatedDamage.Clear();
    }

    private UnitPhaseAccumulatedDamage GetAccumulatedDamage(IUnit unit)
    {
        if (_accumulatedDamage.TryGetValue(unit.Id, out var accumulated)) return accumulated;
        accumulated = new UnitPhaseAccumulatedDamage();
        _accumulatedDamage[unit.Id] = accumulated;
        return accumulated;
    }

    /// <summary>
    /// What a unit took during the phase, for the piloting skill rolls at its end.
    /// </summary>
    private record UnitPhaseAccumulatedDamage
    {
        public List<ComponentHitData> ComponentHits { get; } = [];
        public HashSet<PartLocation> DestroyedParts { get; } = [];
        public List<PilotingSkillRollContext> RollContexts { get; } = [];
    }
}
