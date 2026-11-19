using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Builds the physical-attack resolution queue: every declared attack in resolution order, one
/// item per declared limb (or one for a paired-arm attack such as Push), one per charge and one
/// per DFA. Extracted from <see cref="PhysicalAttackResolutionPhase"/>.
/// </summary>
internal sealed class PhysicalAttackResolutionQueueBuilder
{
    private readonly ServerGame _game;
    private readonly IReadOnlyList<IPhysicalAttackResolutionGate> _gates;

    public PhysicalAttackResolutionQueueBuilder(
        ServerGame game,
        IReadOnlyList<IPhysicalAttackResolutionGate> gates)
    {
        _game = game;
        _gates = gates;
    }

    /// <summary>
    /// The lower Initiative resolves first (Total Warfare p. 144), so a charge or DFA keeps its
    /// attacker's slot instead of always resolving after the limb attacks. All the gates run here,
    /// before any damage of the phase; the attacker-side movement-attack checks use the state at
    /// phase entry, so a charge whose attacker is shut down, unconscious, prone or away from its
    /// approach position still appears here and fails explicitly instead of silently disappearing.
    /// </summary>
    public List<ResolutionQueueItem> Build()
    {
        var queue = new List<ResolutionQueueItem>();
        if (!_game.InitiativeOrder
                .SelectMany(player => player.Units)
                .Any(unit => unit.DeclaredPhysicalAttack != null))
            return queue;

        // The phase only starts on a configured map; this is an entry invariant, not a
        // recoverable state.
        var map = _game.BattleMap ?? throw new InvalidOperationException("Battle map is null");
        var allUnits = _game.Players.SelectMany(p => p.Units).ToList();

        foreach (var player in _game.InitiativeOrder.Reverse())
        {
            foreach (var attacker in player.Units)
            {
                var declaration = attacker.DeclaredPhysicalAttack;
                if (declaration is null) continue;

                queue.AddRange(CreateItems(player, attacker, declaration, allUnits, map));
            }
        }

        return queue;
    }

    /// <summary>
    /// The queue items one declaration produces: a single charge item, a single DFA item, or one
    /// item per declared limb. A movement-origin attack has no limb definition.
    /// </summary>
    private List<ResolutionQueueItem> CreateItems(
        IPlayer player,
        IUnit attacker,
        PhysicalAttackDeclarationData declaration,
        IReadOnlyList<IUnit> allUnits,
        IBattleMap map)
    {
        if (declaration.AttackType == PhysicalAttackType.Charge)
            return
            [
                new ResolutionQueueItem(
                    null,
                    new ChargeQueueItem(
                        player,
                        attacker,
                        declaration,
                        _game.MovementAttackValidator.GetAttackerState(attacker, declaration)),
                    null)
            ];

        if (declaration.AttackType == PhysicalAttackType.DFA)
            return CreateDfaQueueItem(player, attacker, declaration) is { } dfa
                ? [new ResolutionQueueItem(null, null, dfa)]
                : [];

        return CreateLimbItems(player, attacker, declaration, allUnits, map);
    }

    /// <summary>
    /// A paired-arm attack such as Push resolves once for the whole declaration; a punch still
    /// enqueues one independent item per declared arm. Every item runs the pre-attack gates here,
    /// before any damage of the phase.
    /// </summary>
    private List<ResolutionQueueItem> CreateLimbItems(
        IPlayer player,
        IUnit attacker,
        PhysicalAttackDeclarationData declaration,
        IReadOnlyList<IUnit> allUnits,
        IBattleMap map)
    {
        var items = new List<ResolutionQueueItem>();
        var target = allUnits.FirstOrDefault(u => u.Id == declaration.TargetId);
        if (target == null)
        {
            _game.Logger.LogWarning(
                "Skipping {AttackType} by {AttackerName}: target {TargetId} is not in the game",
                declaration.AttackType, attacker.Name, declaration.TargetId);
            return items;
        }

        if (!PhysicalAttackDefinitions.TryGet(declaration.AttackType, out var definition)) return items;
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
            if (_gates.Any(gate => gate.ShouldSkip(context, _game.Logger))) continue;

            items.Add(new ResolutionQueueItem(new AttackQueueItem(player, context), null, null));
        }

        return items;
    }

    /// <summary>
    /// Snapshots a DFA's movement origin, approach hex and declared target hex at queue-build time.
    /// A declaration whose movement path is gone has nothing left to place, so it is the only
    /// build-time skip. A destroyed attacker never reaches this point with a DFA declaration:
    /// <see cref="Unit.Status"/> clears it when the unit is destroyed.
    /// </summary>
    private static DfaQueueItem? CreateDfaQueueItem(
        IPlayer player,
        IUnit attacker,
        PhysicalAttackDeclarationData declaration)
    {
        // A missing path has nothing left to place, so there is no resolution to publish. This is
        // a data guard, not an eligibility rule: whether the attack can still be made is decided
        // by the validator at resolution time.
        if (attacker.MovementTaken is not { } movement) return null;

        var approach = movement.GetStopPosition(declaration);
        if (approach is null) return null;

        return new DfaQueueItem(
            player,
            attacker,
            declaration,
            movement.Start.Coordinates,
            approach);
    }
}
