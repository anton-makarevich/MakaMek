using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Applies push displacements, including the mutual-push matrix and the water-entry modifier of
/// the push PSR. Extracted from <see cref="PhysicalAttackResolutionPhase"/>.
/// </summary>
internal sealed class PhysicalAttackPushResolver
{
    private readonly ServerGame _game;
    private readonly PhysicalAttackPhaseState _state;
    private readonly PushDisplacementResolver _pushDisplacementResolver;
    private readonly PhysicalAttackDisplacementService _displacement;

    public PhysicalAttackPushResolver(
        ServerGame game,
        PhysicalAttackPhaseState state,
        PushDisplacementResolver pushDisplacementResolver,
        PhysicalAttackDisplacementService displacement)
    {
        _game = game;
        _state = state;
        _pushDisplacementResolver = pushDisplacementResolver;
        _displacement = displacement;
    }

    /// <summary>
    /// The pushes in the queue whose attacker and target are swapped by another queued push.
    /// Only attacks that made it through the gates count, so an invalid push never pairs.
    /// </summary>
    public static HashSet<(Guid AttackerId, Guid TargetId)> GetReciprocalPushPairs(
        IReadOnlyList<ResolutionQueueItem> queue)
    {
        var pushKeys = queue
            .Where(item => item.Attack?.Context.AttackType == PhysicalAttackType.Push)
            .Select(item => (
                AttackerId: item.Attack!.Value.Context.Attacker.Id,
                TargetId: item.Attack.Value.Context.Target.Id))
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
    /// Applies the mutual-push matrix (Total Warfare, p. 148) once both rolls of a pair are known:
    /// if both pushes hit, neither Mech moves and both keep their piloting skill rolls; if only
    /// one hits, it is a standard push. Only reciprocal pairs are deferred this way — every other
    /// push displaces immediately after its own attack. Pairs are applied in resolution order and
    /// every destination is evaluated from the positions at application time.
    /// </summary>
    public void ApplyReciprocal(IReadOnlyList<PushOutcome> hits)
    {
        var hitKeys = hits.Select(push => (push.Attacker.Id, push.Target.Id)).ToHashSet();

        foreach (var push in hits)
        {
            if (hitKeys.Contains((push.Target.Id, push.Attacker.Id)))
                continue; // Both pushes hit: neither unit moves.

            ApplyDisplacement(push.Attacker, push.Target);
        }
    }

    public void ApplyDisplacement(IUnit attacker, IUnit target)
    {
        var map = _game.BattleMap;
        if (map is null) return;

        var result = _pushDisplacementResolver.Resolve(
            attacker, target, map, _displacement.AllUnits(), _game.Id);
        if (result is null) return; // Blocked or no longer a valid push: the target PSR still happens.

        // Apply the domino chain first, far-to-near, so each link moves into a vacated hex. A chain
        // stopped by a later link keeps its earlier displacements.
        if (result.DominoEffect is { HasDisplacements: true } domino)
        {
            foreach (var chainCommand in domino.Commands)
                _displacement.Execute(new DisplaceUnitAction(chainCommand with { GameOriginId = _game.Id }, publish: true));
        }

        _displacement.Execute(new DisplaceUnitAction(
            new DisplaceUnitCommand
            {
                UnitId = target.Id,
                FromCoordinates = target.Position!.Coordinates.ToData(),
                ToCoordinates = result.TargetDestination.Coordinates.ToData(),
                NewFacing = (int)result.TargetDestination.Facing,
                Surface = (int)result.TargetDestination.Surface,
                DisplacementReason = DisplacementReason.Push,
                GameOriginId = _game.Id,
                Timestamp = DateTime.UtcNow
            },
            publish: true));

        // A push into water is not an extra roll: the water-entry modifier belongs to the push PSR.
        ApplyWaterEntryModifier(target, map);

        _displacement.Execute(new DisplaceUnitAction(
            new DisplaceUnitCommand
            {
                UnitId = attacker.Id,
                FromCoordinates = attacker.Position!.Coordinates.ToData(),
                ToCoordinates = result.AttackerDestination.Coordinates.ToData(),
                NewFacing = (int)result.AttackerDestination.Facing,
                Surface = (int)result.AttackerDestination.Surface,
                DisplacementReason = DisplacementReason.PushAdvance,
                GameOriginId = _game.Id,
                Timestamp = DateTime.UtcNow
            },
            publish: true));

        // Every unit involved in the domino rolls once all displacement is applied, the push target
        // included: its PushHit roll covers the push, not the domino (Total Warfare p. 152). Water
        // entry is not a second roll either - it is a modifier on the push's own PSR.
        if (result.DominoEffect is { HasDisplacements: true } resolvedDomino)
        {
            _displacement.Execute(new DominoEffectRollAction(
                resolvedDomino.PsrCandidates, _game.FallProcessor, resolvedDomino.Falls));
        }

        if (result.TriggersAutomaticFall && target is Mech targetMech)
            _displacement.ResolveAutomaticFall(targetMech, result.LevelsFallen);
    }

    /// <summary>
    /// Adds the standard water-entry modifier to the PushHit PSR when the target was displaced
    /// into water. Entering water is not a separate roll (Total Warfare, p. 148, 151).
    /// </summary>
    private void ApplyWaterEntryModifier(IUnit target, IBattleMap map)
    {
        if (!_state.PushHitContexts.TryGetValue(target.Id, out var entry)) return;
        if (target.Position is not { Surface: HexSurface.Ground } position) return;

        var waterDepth = map.GetHex(position.Coordinates)?.GetWaterDepth() ?? 0;
        if (waterDepth < 1) return;

        var index = entry.Contexts.IndexOf(entry.Context);
        if (index >= 0)
            entry.Contexts[index] = entry.Context with { WaterDepth = waterDepth };
    }
}
