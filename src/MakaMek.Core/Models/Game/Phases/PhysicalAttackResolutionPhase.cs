using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Resolves the physical attacks declared this turn, following the weapon attack resolution
/// pattern: a queue in initiative order with one item per declared limb (or one item for a
/// paired-arm attack such as Push), the pre-attack gates, the resolver, then criticals,
/// consciousness rolls and the end-of-phase piloting skill rolls.
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
    private readonly Dictionary<Guid, (List<PilotingSkillRollContext> Contexts, PushHitRollContext Context)>
        _pushHitContexts = new();
    private readonly PushDisplacementResolver _pushDisplacementResolver = new(game.RulesProvider, game.DiceRoller);

    private readonly record struct AttackQueueItem(IPlayer Player, PhysicalAttackContext Context);

    private sealed record PushOutcome(IUnit Attacker, IUnit Target, bool IsHit, bool IsReciprocal);

    public override void Enter()
    {
        base.Enter();
        _accumulatedDamage.Clear();
        _pushHitContexts.Clear();

        var queue = BuildAttackQueue();
        var reciprocalPushes = GetReciprocalPushPairs(queue);
        List<PushOutcome> pushOutcomes = [];

        foreach (var item in queue)
        {
            var resolution = ResolveAttack(item);
            if (item.Context.AttackType != PhysicalAttackType.Push) continue;

            pushOutcomes.Add(new PushOutcome(
                item.Context.Attacker,
                item.Context.Target,
                resolution.AttackResolution.IsHit,
                reciprocalPushes.Contains((item.Context.Attacker.Id, item.Context.Target.Id))));
        }

        // Displacement is resolved once every attack of the phase is known, so mutual pushes can
        // be resolved as a pair before any unit moves.
        ApplyPushDisplacements(pushOutcomes);

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

            // A paired-arm attack such as Push resolves once for the whole declaration; a punch
            // still enqueues one independent item per declared arm.
            var definition = PhysicalAttackDefinitions.Get(declaration.AttackType);
            var limbs = definition.ResolvesAsSingleAttack
                ? declaration.AttackerLimbs.Take(1)
                : declaration.AttackerLimbs;

            foreach (var limb in limbs)
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

    private PhysicalAttackResolutionData ResolveAttack(AttackQueueItem item)
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

        foreach (var requirement in resolution.PsrRequirements)
        {
            var recipient = requirement.Recipient switch
            {
                PhysicalAttackParticipant.Target => target,
                PhysicalAttackParticipant.Attacker => item.Context.Attacker,
                _ => throw new NotSupportedException(
                    $"No piloting skill roll recipient is defined for {requirement.Recipient}")
            };
            var contexts = GetAccumulatedDamage(recipient).RollContexts;
            var context = GetRollContext(requirement.RollType);
            contexts.Add(context);

            // The push PSR is written before the displacement is applied. Keep a handle on it so
            // the water-entry modifier can be added once the destination is known.
            if (context is PushHitRollContext pushHitContext)
                _pushHitContexts[recipient.Id] = (contexts, pushHitContext);
        }

        return resolution;
    }

    private static PilotingSkillRollContext GetRollContext(PilotingSkillRollType rollType) => rollType switch
    {
        PilotingSkillRollType.KickHit => new KickHitRollContext(),
        PilotingSkillRollType.KickMiss => new KickMissRollContext(),
        PilotingSkillRollType.PushHit => new PushHitRollContext(),
        _ => throw new NotSupportedException($"No piloting skill roll context is defined for {rollType}")
    };

    /// <summary>
    /// The pushes in the queue whose attacker and target are swapped by another queued push.
    /// Only attacks that made it through the gates count, so an invalid push never pairs.
    /// </summary>
    private static HashSet<(Guid AttackerId, Guid TargetId)> GetReciprocalPushPairs(
        IReadOnlyList<AttackQueueItem> queue)
    {
        var pushKeys = queue
            .Where(item => item.Context.AttackType == PhysicalAttackType.Push)
            .Select(item => (AttackerId: item.Context.Attacker.Id, TargetId: item.Context.Target.Id))
            .ToHashSet();

        var reciprocal = new HashSet<(Guid AttackerId, Guid TargetId)>();
        foreach (var key in pushKeys)
        {
            if (pushKeys.Contains((key.TargetId, key.AttackerId)))
                reciprocal.Add(key);
        }

        return reciprocal;
    }

    /// <summary>
    /// Applies the mutual-push matrix (Total Warfare, p. 148): if both pushes in a pair hit,
    /// neither Mech moves and both keep their piloting skill rolls; if only one hits, it is a
    /// standard push; if both miss, nothing happens. Unpaired hits are standard pushes. Pushes
    /// are applied in queue (initiative) order and every destination is evaluated from the
    /// positions at application time.
    /// </summary>
    private void ApplyPushDisplacements(IReadOnlyList<PushOutcome> outcomes)
    {
        var outcomesByAttack = outcomes.ToDictionary(
            outcome => (outcome.Attacker.Id, outcome.Target.Id));

        foreach (var outcome in outcomes)
        {
            if (!outcome.IsHit) continue;

            if (outcome.IsReciprocal
                && outcomesByAttack.GetValueOrDefault((outcome.Target.Id, outcome.Attacker.Id)) is { IsHit: true })
                continue; // Both pushes hit: neither unit moves.

            ApplyPushDisplacement(outcome.Attacker, outcome.Target);
        }
    }

    private void ApplyPushDisplacement(IUnit attacker, IUnit target)
    {
        var map = Game.BattleMap;
        if (map is null) return;

        var result = _pushDisplacementResolver.Resolve(attacker, target, map, AllUnits(), Game.Id);
        if (result is null) return; // Blocked or no longer a valid push: the target PSR still happens.

        // Apply the domino chain first, far-to-near, so each link moves into a vacated hex. A chain
        // stopped by a later link keeps its earlier displacements.
        if (result.DominoEffect is { HasDisplacements: true } domino)
        {
            foreach (var chainCommand in domino.Commands)
                Execute(new DisplaceUnitAction(chainCommand with { GameOriginId = Game.Id }, publish: true));
        }

        Execute(new DisplaceUnitAction(
            new DisplaceUnitCommand
            {
                UnitId = target.Id,
                FromCoordinates = target.Position!.Coordinates.ToData(),
                ToCoordinates = result.TargetDestination.Coordinates.ToData(),
                NewFacing = (int)result.TargetDestination.Facing,
                Surface = (int)result.TargetDestination.Surface,
                DisplacementReason = DisplacementReason.Push,
                GameOriginId = Game.Id,
                Timestamp = DateTime.UtcNow
            },
            publish: true));

        // A push into water is not an extra roll: the water-entry modifier belongs to the push PSR.
        ApplyPushWaterEntryModifier(target, map);

        Execute(new DisplaceUnitAction(
            new DisplaceUnitCommand
            {
                UnitId = attacker.Id,
                FromCoordinates = attacker.Position!.Coordinates.ToData(),
                ToCoordinates = result.AttackerDestination.Coordinates.ToData(),
                NewFacing = (int)result.AttackerDestination.Facing,
                Surface = (int)result.AttackerDestination.Surface,
                DisplacementReason = DisplacementReason.PushAdvance,
                GameOriginId = Game.Id,
                Timestamp = DateTime.UtcNow
            },
            publish: true));

        // Every unit involved in the domino rolls once all displacement is applied, the push target
        // included: its PushHit roll covers the push, not the domino (Total Warfare p. 152). Water
        // entry is not a second roll either - it is a modifier on the push's own PSR.
        if (result.DominoEffect is { HasDisplacements: true } resolvedDomino)
        {
            Execute(new DominoEffectRollAction(
                resolvedDomino.PsrCandidates, Game.FallProcessor, resolvedDomino.Falls));
        }

        if (result.TriggersAutomaticFall && target is Mech targetMech)
            ResolveAutomaticFall(targetMech, result.LevelsFallen);
    }

    /// <summary>
    /// Adds the standard water-entry modifier to the PushHit PSR when the target was displaced
    /// into water. Entering water is not a separate roll (Total Warfare, p. 148, 151).
    /// </summary>
    private void ApplyPushWaterEntryModifier(IUnit target, IBattleMap map)
    {
        if (!_pushHitContexts.TryGetValue(target.Id, out var entry)) return;
        if (target.Position is not { Surface: HexSurface.Ground } position) return;

        var waterDepth = map.GetHex(position.Coordinates)?.GetWaterDepth() ?? 0;
        if (waterDepth < 1) return;

        var index = entry.Contexts.IndexOf(entry.Context);
        if (index >= 0)
            entry.Contexts[index] = entry.Context with { WaterDepth = waterDepth };
    }

    /// <summary>
    /// A push that drops the target more than two levels is allowed and ends in an automatic
    /// fall (Total Warfare, p. 148, 151). The fall lands in the destination hex; a unit that
    /// becomes prone there skips its later PushHit roll.
    /// </summary>
    private void ResolveAutomaticFall(Mech mech, int levelsFallen)
    {
        if (mech.Position is not { } position) return;

        var facingRoll = Game.DiceRoller.RollD6();
        var facingAfterFall = Game.RulesProvider.GetFacingAfterFall(facingRoll.Result, position.Facing);
        var fallContext = Game.FallProcessor.ProcessMovementAttempt(
            mech,
            new CliffFallRollContext(levelsFallen, facingRoll, facingAfterFall),
            Game,
            MovementType.StandingStill,
            position);

        if (fallContext.IsFalling)
            Execute(new ApplyFallAction(mech, fallContext.ToMechFallCommand()));
    }

    private void Execute(IGameAction action)
    {
        foreach (var command in action.Process(Game))
            Game.CommandPublisher.PublishCommand(command);
    }

    private IEnumerable<IUnit> AllUnits() => Game.Players.SelectMany(player => player.Units);

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
