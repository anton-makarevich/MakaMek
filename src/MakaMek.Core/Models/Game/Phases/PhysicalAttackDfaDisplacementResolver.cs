using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Applies the displacements caused by a DFA hit or miss. Extracted from
/// <see cref="PhysicalAttackDfaResolver"/>.
/// </summary>
internal sealed class PhysicalAttackDfaDisplacementResolver
{
    private readonly PhysicalAttackDisplacementService _displacement;
    private readonly PhysicalAttackDfaLandingResolver _landing;

    public PhysicalAttackDfaDisplacementResolver(
        PhysicalAttackDisplacementService displacement,
        PhysicalAttackDfaLandingResolver landing)
    {
        _displacement = displacement;
        _landing = landing;
    }

    public void ApplyHit(
        DfaQueueItem item,
        IUnit target,
        DfaDisplacementResult? displacement)
    {
        if (displacement is null) return;

        // A target destroyed by the impact does not move, so the displacement plan prepared for it
        // is discarded: its domino must not move other units or roll their PSRs. The target is also
        // not pushed when the impact destroyed the attacker (Total Warfare, p. 150).
        var attackerDestroyed = item.Attacker.IsDestroyed;
        var targetMoves = !target.IsDestroyed
                          && !attackerDestroyed
                          && displacement.TargetDestination is not null;
        if (targetMoves)
        {
            _displacement.ExecuteDominoCommands(displacement.DominoEffect);
            _displacement.ExecuteDisplacement(
                target,
                displacement.TargetDestination!,
                DisplacementReason.DfaTarget);

            if (displacement.TriggersAutomaticFall && target is Mech targetMech)
                _displacement.ResolveAutomaticFall(targetMech, displacement.LevelsFallen);
        }

        // A destroyed attacker is removed from the board instead of grounding in the target hex.
        if (attackerDestroyed) return;

        var landing = _landing.Execute(item.Attacker, DfaLandingKind.AttackerLanding);
        _displacement.RollDominoEffects(
            targetMoves ? displacement.DominoEffect : null,
            landing?.DominoEffect);
    }

    /// <summary>
    /// Grounds the attacker after a miss: the target moves to the hex the resolver picked along the
    /// jump vector, then the attacker completes its jump in the target's original hex and falls two
    /// levels with rear hit locations.
    /// </summary>
    public void ApplyMiss(
        IUnit attacker,
        IUnit target,
        DfaMissDisplacementCandidate candidate,
        DisplacementReason reason)
    {
        if (target.Position is { } currentPosition)
        {
            _displacement.ExecuteDisplacement(
                target,
                new HexPosition(candidate.Coordinates, currentPosition.Facing, candidate.Surface),
                reason);
        }

        if (candidate.TriggersAutomaticFall && target is Mech targetMech)
            _displacement.ResolveAutomaticFall(targetMech, candidate.LevelsFallen);

        var landing = _landing.Execute(attacker);
        _displacement.RollDominoEffects(landing?.DominoEffect);
        _landing.ApplyFall(attacker);
    }
}
