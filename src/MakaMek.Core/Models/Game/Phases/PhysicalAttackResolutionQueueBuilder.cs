using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Every declared physical attack of the phase in resolution order: one item per declared limb
/// (or one for a paired-arm attack such as Push) and one per charge. Extracted from
/// <see cref="PhysicalAttackResolutionPhase"/> so the phase only walks the queue.
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
    /// The lower Initiative resolves first (Total Warfare p. 144), so a charge keeps its attacker's
    /// slot instead of always resolving after the limb attacks. All the gates run here, before any
    /// damage of the phase; the attacker-side charge checks use the state at phase entry, so a
    /// charge whose attacker is shut down, unconscious, prone or away from its approach position
    /// still appears here and fails explicitly instead of silently disappearing.
    /// </summary>
    public List<ResolutionQueueItem> Build()
    {
        var queue = new List<ResolutionQueueItem>();
        if (!HasAnyDeclaration()) return queue;

        var map = _game.BattleMap ?? throw new InvalidOperationException("Battle map is null");
        var allUnits = _game.Players.SelectMany(p => p.Units).ToList();

        foreach (var player in _game.InitiativeOrder.Reverse())
        {
            foreach (var attacker in player.Units)
            {
                var declaration = attacker.DeclaredPhysicalAttack;
                if (declaration is null) continue;

                if (declaration.AttackType == PhysicalAttackType.Charge)
                {
                    queue.Add(new ResolutionQueueItem(null, new ChargeQueueItem(
                        player,
                        attacker,
                        declaration.TargetId,
                        declaration.HexesMoved ?? 0,
                        IsAttackerAutoFail(attacker, declaration))));
                    continue;
                }

                AddAttackItems(queue, player, attacker, declaration, allUnits, map);
            }
        }

        return queue;
    }

    private bool HasAnyDeclaration() =>
        _game.InitiativeOrder
            .SelectMany(player => player.Units)
            .Any(unit => unit.DeclaredPhysicalAttack != null);

    private void AddAttackItems(
        List<ResolutionQueueItem> queue,
        IPlayer player,
        IUnit attacker,
        PhysicalAttackDeclarationData declaration,
        List<IUnit> allUnits,
        IBattleMap map)
    {
        var target = allUnits.FirstOrDefault(u => u.Id == declaration.TargetId);
        if (target == null)
        {
            _game.Logger.LogWarning(
                "Skipping {AttackType} by {AttackerName}: target {TargetId} is not in the game",
                declaration.AttackType, attacker.Name, declaration.TargetId);
            return;
        }

        // A paired-arm attack such as Push resolves once for the whole declaration; a punch
        // still enqueues one independent item per declared arm. A movement-origin attack
        // (Charge or DFA) has no definition yet: its resolution mechanics land in a later
        // slice, so it must be skipped instead of throwing here.
        if (!PhysicalAttackDefinitions.TryGet(declaration.AttackType, out var definition)) return;
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

            queue.Add(new ResolutionQueueItem(new AttackQueueItem(player, context), null));
        }
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
}
