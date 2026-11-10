using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;
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
    private readonly ChargeDisplacementResolver _chargeDisplacementResolver = new(game.RulesProvider, game.DiceRoller);

    private readonly record struct AttackQueueItem(IPlayer Player, PhysicalAttackContext Context);

    private sealed record ChargeQueueItem(
        IPlayer Player,
        IUnit Attacker,
        Guid DeclaredTargetId,
        int HexesMoved,
        bool AttackerAutoFail);

    private sealed record PushOutcome(IUnit Attacker, IUnit Target, bool IsHit, bool IsReciprocal);

    public override void Enter()
    {
        base.Enter();
        _accumulatedDamage.Clear();
        _pushHitContexts.Clear();

        // Snapshot the charges before any damage: a charge must still resolve when an earlier
        // attack of the phase destroys its attacker or target, and Unit clears a destroyed unit's
        // pending declaration.
        var chargeQueue = BuildChargeQueue();
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

        // Charges resolve after ordinary attacks and push displacement, so the replacement-target
        // rule sees the units that earlier displacements moved.
        ResolveCharges(chargeQueue);

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

            // Charge has no limb-based definition and is resolved by the charge mechanics after
            // the ordinary attacks. Skip it before the missing-target log, which stays for the
            // limb-based attacks.
            if (declaration.AttackType == PhysicalAttackType.Charge) continue;

            var target = allUnits.FirstOrDefault(u => u.Id == declaration.TargetId);
            if (target == null)
            {
                Game.Logger.LogWarning("Skipping {AttackType} by {AttackerName}: target {TargetId} is not in the game",
                    declaration.AttackType, attacker.Name, declaration.TargetId);
                continue;
            }

            // A paired-arm attack such as Push resolves once for the whole declaration; a punch
            // still enqueues one independent item per declared arm. A movement-origin attack
            // (Charge or DFA) has no definition yet: its resolution mechanics land in a later
            // slice, so it must be skipped instead of throwing here.
            if (!PhysicalAttackDefinitions.TryGet(declaration.AttackType, out var definition)) continue;
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

    /// <summary>
    /// Snapshots every declared charge in initiative order before any damage of the phase. The
    /// attacker-side automatic-failure checks use the state at phase entry, so a charge whose
    /// attacker is shut down, unconscious, prone or away from its approach position still appears
    /// here and fails explicitly instead of silently disappearing.
    /// </summary>
    private List<ChargeQueueItem> BuildChargeQueue()
    {
        List<ChargeQueueItem> queue = [];
        foreach (var player in Game.InitiativeOrder)
        {
            foreach (var unit in player.Units)
            {
                var declaration = unit.DeclaredPhysicalAttack;
                if (declaration is not { AttackType: PhysicalAttackType.Charge }) continue;

                queue.Add(new ChargeQueueItem(
                    player,
                    unit,
                    declaration.TargetId,
                    declaration.HexesMoved ?? 0,
                    IsAttackerAutoFail(unit, declaration)));
            }
        }

        return queue;
    }

    private static bool IsAttackerAutoFail(IUnit attacker, PhysicalAttackDeclarationData declaration)
    {
        if (attacker.IsDestroyed
            || attacker.IsShutdown
            || attacker.Pilot is not { IsConscious: true }
            || attacker.Status.HasFlag(UnitStatus.Prone))
            return true;

        if (attacker.MovementTaken is not { } movement) return true;

        var approach = movement.GetStopPosition(declaration);
        return approach is null || attacker.Position != approach;
    }

    private PhysicalAttackResolutionData ResolveAttack(AttackQueueItem item)
    {
        var target = item.Context.Target;
        var attacker = item.Context.Attacker;
        var resolution = Game.PhysicalAttackResolver.ResolveAttack(item.Context);

        var targetDestroyedPartsBefore = DestroyedParts(target);
        var targetWasDestroyedBefore = target.IsDestroyed;
        var attackerDestroyedPartsBefore = DestroyedParts(attacker);
        var attackerWasDestroyedBefore = attacker.IsDestroyed;

        var command = new PhysicalAttackResolutionCommand
        {
            ResolutionId = Guid.NewGuid(),
            GameOriginId = Game.Id,
            PlayerId = item.Player.Id,
            AttackerId = attacker.Id,
            TargetId = target.Id,
            ResolutionData = resolution
        };
        // The server transport does not loop commands back to the server game.
        // Apply the authoritative result before publishing it to clients.
        Game.OnPhysicalAttackResolution(command);

        resolution = AttachDestructionMetadata(
            resolution,
            target, targetDestroyedPartsBefore, targetWasDestroyedBefore,
            attacker, attackerDestroyedPartsBefore, attackerWasDestroyedBefore);
        Game.CommandPublisher.PublishCommand(command with { ResolutionData = resolution });

        ApplyDamageConsequences(target, resolution.AttackResolution);
        ApplyPsrRequirements(attacker, target, resolution.PsrRequirements);

        return resolution;
    }

    private void ResolveCharges(IReadOnlyList<ChargeQueueItem> charges)
    {
        foreach (var charge in charges)
            ResolveCharge(charge);
    }

    private void ResolveCharge(ChargeQueueItem item)
    {
        var targetSelection = item.AttackerAutoFail
            ? ChargeTargetSelection.AutoFail()
            : ChargeTargetSelector.Select(item.Attacker, item.DeclaredTargetId, AllUnits());

        if (targetSelection.Target is not { } target)
        {
            ResolveChargeAutoFail(item, targetSelection);
            return;
        }

        var attacker = item.Attacker;
        var resolution = Game.PhysicalAttackResolver.ResolveCharge(new ChargeAttackContext
        {
            Attacker = attacker,
            Target = target,
            Map = Game.BattleMap ?? throw new InvalidOperationException("Battle map is null"),
            HexesMoved = item.HexesMoved,
            IsReplacementTarget = targetSelection.IsReplacementTarget,
            OriginalTargetId = item.DeclaredTargetId
        });

        var targetDestroyedPartsBefore = DestroyedParts(target);
        var targetWasDestroyedBefore = target.IsDestroyed;
        var attackerDestroyedPartsBefore = DestroyedParts(attacker);
        var attackerWasDestroyedBefore = attacker.IsDestroyed;

        var command = new PhysicalAttackResolutionCommand
        {
            ResolutionId = Guid.NewGuid(),
            GameOriginId = Game.Id,
            PlayerId = item.Player.Id,
            AttackerId = attacker.Id,
            TargetId = target.Id,
            ResolutionData = resolution
        };
        Game.OnPhysicalAttackResolution(command);

        resolution = AttachDestructionMetadata(
            resolution,
            target, targetDestroyedPartsBefore, targetWasDestroyedBefore,
            attacker, attackerDestroyedPartsBefore, attackerWasDestroyedBefore);
        Game.CommandPublisher.PublishCommand(command with { ResolutionData = resolution });

        ApplyDamageConsequences(target, resolution.AttackResolution);
        if (resolution.AttackResolution.IsHit)
            ApplyDamageConsequences(
                attacker,
                resolution.Charge?.AttackerCollision,
                resolution.Charge?.AttackerDestroyedParts);

        ApplyPsrRequirements(attacker, target, resolution.PsrRequirements);

        switch (resolution.Charge?.Outcome)
        {
            case ChargeOutcome.Hit:
                ApplyChargeHitDisplacement(attacker, target);
                break;
            case ChargeOutcome.Miss:
                ApplyChargeMissDisplacement(attacker);
                break;
        }
    }

    /// <summary>
    /// Publishes the automatic failure of a charge. No roll is made and no damage or piloting
    /// skill roll is created. On a target-side failure the attacker completes its committed
    /// movement into the destination hex unless another non-destroyed unit occupies it; on an
    /// attacker-side failure the attacker stays where it is.
    /// </summary>
    private void ResolveChargeAutoFail(ChargeQueueItem item, ChargeTargetSelection selection)
    {
        var resolution = new PhysicalAttackResolutionData(
            PhysicalAttackType.Charge,
            null,
            0,
            new AttackResolutionData(0, [], false, HitDirection.Front, 0),
            [],
            new ChargeResolutionData(
                ChargeOutcome.AutoFail,
                item.HexesMoved,
                false,
                item.DeclaredTargetId));

        var command = new PhysicalAttackResolutionCommand
        {
            ResolutionId = Guid.NewGuid(),
            GameOriginId = Game.Id,
            PlayerId = item.Player.Id,
            AttackerId = item.Attacker.Id,
            TargetId = item.DeclaredTargetId,
            ResolutionData = resolution
        };
        Game.OnPhysicalAttackResolution(command);
        Game.CommandPublisher.PublishCommand(command);

        if (item.AttackerAutoFail || selection.BlocksAttackerEntry) return;

        if (_chargeDisplacementResolver.ResolveAdvance(item.Attacker) is { AttackerDestination: { } destination })
            ExecuteDisplacement(item.Attacker, destination, DisplacementReason.ChargeAdvance);
    }

    private void ApplyChargeHitDisplacement(IUnit attacker, IUnit target)
    {
        var map = Game.BattleMap;
        if (map is null) return;

        var result = _chargeDisplacementResolver.ResolveHit(
            attacker, target, map, AllUnits(), Game.Id);
        if (result is null) return;

        ApplyChargeDisplacement(attacker, target, result);
    }

    private void ApplyChargeMissDisplacement(IUnit attacker)
    {
        var map = Game.BattleMap;
        if (map is null) return;

        var result = _chargeDisplacementResolver.ResolveMiss(attacker, map, AllUnits());
        if (result?.AttackerDestination is { } destination)
            ExecuteDisplacement(attacker, destination, DisplacementReason.ChargeMiss);
    }

    private void ApplyChargeDisplacement(IUnit attacker, IUnit target, ChargeDisplacementResult result)
    {
        if (result.DominoEffect is { HasDisplacements: true } domino)
        {
            foreach (var chainCommand in domino.Commands)
                Execute(new DisplaceUnitAction(chainCommand with { GameOriginId = Game.Id }, publish: true));
        }

        if (result.TargetDestination is { } targetDestination)
            ExecuteDisplacement(target, targetDestination, DisplacementReason.Charge);

        if (result.AttackerDestination is { } attackerDestination)
            ExecuteDisplacement(attacker, attackerDestination, DisplacementReason.ChargeAdvance);

        if (result.DominoEffect is { HasDisplacements: true } resolvedDomino)
        {
            Execute(new DominoEffectRollAction(
                resolvedDomino.PsrCandidates, Game.FallProcessor, resolvedDomino.Falls));
        }

        if (result.TriggersAutomaticFall && target is Mech targetMech)
            ResolveAutomaticFall(targetMech, result.LevelsFallen);
    }

    private void ExecuteDisplacement(IUnit unit, HexPosition destination, DisplacementReason reason)
    {
        if (unit.Position is not { } from) return;

        Execute(new DisplaceUnitAction(
            new DisplaceUnitCommand
            {
                UnitId = unit.Id,
                FromCoordinates = from.Coordinates.ToData(),
                ToCoordinates = destination.Coordinates.ToData(),
                NewFacing = (int)destination.Facing,
                Surface = (int)destination.Surface,
                DisplacementReason = reason,
                GameOriginId = Game.Id,
                Timestamp = DateTime.UtcNow
            },
            publish: true));
    }

    /// <summary>
    /// Applies the hull breach, critical hits and consciousness consequences of a unit's damage
    /// and accumulates the component hits and destroyed parts for the end-of-phase rolls. A charge
    /// calls it for the target and, on a hit, for the attacker as well.
    /// </summary>
    private void ApplyDamageConsequences(IUnit unit, AttackResolutionData resolution) =>
        ApplyDamageConsequences(unit, resolution.HitLocationsData, resolution.DestroyedParts);

    private void ApplyDamageConsequences(
        IUnit unit,
        AttackHitLocationsData? hitLocationsData,
        IReadOnlyList<PartLocation>? destroyedParts = null)
    {
        var damage = hitLocationsData?.HitLocations
            .SelectMany(h => h.Damage).ToList() ?? [];
        if (damage.Any(d => d.ArmorDamage > 0 || d.StructureDamage > 0))
        {
            var hullBreachCommand = Game.HullBreachCalculator.CalculateAndApplyHullBreach(unit, damage);
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
            var criticalHitsCommand = Game.CriticalHitsCalculator.CalculateCriticalHits(unit, damage);
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
        ProcessConsciousnessRollsForUnit(unit);

        var accumulatedDamage = GetAccumulatedDamage(unit);
        accumulatedDamage.ComponentHits.AddRange(componentHits);
        foreach (var part in (destroyedParts ?? []).Concat(blownOffParts))
            accumulatedDamage.DestroyedParts.Add(part);
    }

    private void ApplyPsrRequirements(
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
            var contexts = GetAccumulatedDamage(recipient).RollContexts;
            var context = GetRollContext(requirement.RollType);
            contexts.Add(context);

            // The push PSR is written before the displacement is applied. Keep a handle on it so
            // the water-entry modifier can be added once the destination is known.
            if (context is PushHitRollContext pushHitContext)
                _pushHitContexts[recipient.Id] = (contexts, pushHitContext);
        }
    }

    /// <summary>
    /// Records the parts and the destruction a resolution caused on both units. The target's
    /// metadata stays on the attack resolution; a charge's attacker metadata travels in its
    /// charge sub-record so clients can show both units' results.
    /// </summary>
    private static PhysicalAttackResolutionData AttachDestructionMetadata(
        PhysicalAttackResolutionData resolution,
        IUnit target,
        IReadOnlyList<PartLocation> targetDestroyedPartsBefore,
        bool targetWasDestroyedBefore,
        IUnit attacker,
        IReadOnlyList<PartLocation> attackerDestroyedPartsBefore,
        bool attackerWasDestroyedBefore)
    {
        var newlyDestroyedTargetParts = DestroyedParts(target)
            .Except(targetDestroyedPartsBefore).ToList();
        resolution = resolution with
        {
            AttackResolution = resolution.AttackResolution with
            {
                DestroyedParts = newlyDestroyedTargetParts.Count != 0 ? newlyDestroyedTargetParts : null,
                UnitDestroyed = !targetWasDestroyedBefore && target.IsDestroyed
            }
        };

        if (resolution.Charge is not { } charge) return resolution;

        var newlyDestroyedAttackerParts = DestroyedParts(attacker)
            .Except(attackerDestroyedPartsBefore).ToList();
        return resolution with
        {
            Charge = charge with
            {
                AttackerDestroyedParts = newlyDestroyedAttackerParts.Count != 0
                    ? newlyDestroyedAttackerParts
                    : null,
                AttackerUnitDestroyed = !attackerWasDestroyedBefore && attacker.IsDestroyed
            }
        };
    }

    private static List<PartLocation> DestroyedParts(IUnit unit) =>
        unit.Parts.Values.Where(part => part.IsDestroyed).Select(part => part.Location).ToList();

    private static PilotingSkillRollContext GetRollContext(PilotingSkillRollType rollType) => rollType switch
    {
        PilotingSkillRollType.KickHit => new KickHitRollContext(),
        PilotingSkillRollType.KickMiss => new KickMissRollContext(),
        PilotingSkillRollType.PushHit => new PushHitRollContext(),
        PilotingSkillRollType.ChargeImpact => new ChargeImpactRollContext(),
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
