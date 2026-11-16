using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Applies the displacement that follows a charge hit or miss (and the domino chain it can
/// trigger). Extracted from <see cref="PhysicalAttackChargeResolver"/>.
/// </summary>
internal sealed class PhysicalAttackChargeDisplacement
{
    private readonly ServerGame _game;
    private readonly ChargeDisplacementResolver _chargeDisplacementResolver;
    private readonly PhysicalAttackDisplacementService _displacement;

    public PhysicalAttackChargeDisplacement(
        ServerGame game,
        ChargeDisplacementResolver chargeDisplacementResolver,
        PhysicalAttackDisplacementService displacement)
    {
        _game = game;
        _chargeDisplacementResolver = chargeDisplacementResolver;
        _displacement = displacement;
    }

    public void ApplyHit(IUnit attacker, IUnit target)
    {
        var map = _game.BattleMap;
        if (map is null) return;

        var result = _chargeDisplacementResolver.ResolveHit(
            attacker, target, map, _displacement.AllUnits(), _game.Id);
        if (result is null) return;

        Apply(attacker, target, result);
    }

    public void ApplyMiss(IUnit attacker)
    {
        var map = _game.BattleMap;
        if (map is null) return;

        var result = _chargeDisplacementResolver.ResolveMiss(attacker, map, _displacement.AllUnits());
        if (result?.AttackerDestination is { } destination)
            _displacement.ExecuteDisplacement(attacker, destination, DisplacementReason.ChargeMiss);
    }

    private void Apply(IUnit attacker, IUnit target, ChargeDisplacementResult result)
    {
        if (result.DominoEffect is { HasDisplacements: true } domino)
        {
            foreach (var chainCommand in domino.Commands)
                _displacement.Execute(new DisplaceUnitAction(chainCommand with { GameOriginId = _game.Id }, publish: true));
        }

        if (result.TargetDestination is { } targetDestination)
            _displacement.ExecuteDisplacement(target, targetDestination, DisplacementReason.Charge);

        if (result.AttackerDestination is { } attackerDestination)
            _displacement.ExecuteDisplacement(attacker, attackerDestination, DisplacementReason.ChargeAdvance);

        if (result.DominoEffect is { HasDisplacements: true } resolvedDomino)
        {
            _displacement.Execute(new DominoEffectRollAction(
                resolvedDomino.PsrCandidates, _game.FallProcessor, resolvedDomino.Falls));
        }

        if (result.TriggersAutomaticFall && target is Mech targetMech)
            _displacement.ResolveAutomaticFall(targetMech, result.LevelsFallen);
    }
}
