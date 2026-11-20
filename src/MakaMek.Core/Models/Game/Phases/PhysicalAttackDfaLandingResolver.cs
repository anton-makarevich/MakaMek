using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Logger;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Grounds a DFA attacker: resolves the landing hex from its recorded jump path, completes the jump
/// and applies the mandatory miss fall. Extracted from <see cref="PhysicalAttackDfaResolver"/>.
/// </summary>
internal sealed class PhysicalAttackDfaLandingResolver
{
    private readonly ServerGame _game;
    private readonly PhysicalAttackPhaseState _state;
    private readonly DfaDisplacementResolver _dfaDisplacementResolver;
    private readonly PhysicalAttackDisplacementService _displacement;

    public PhysicalAttackDfaLandingResolver(
        ServerGame game,
        PhysicalAttackPhaseState state,
        DfaDisplacementResolver dfaDisplacementResolver,
        PhysicalAttackDisplacementService displacement)
    {
        _game = game;
        _state = state;
        _dfaDisplacementResolver = dfaDisplacementResolver;
        _displacement = displacement;
    }

    /// <summary>
    /// Resolves the landing of the jump path the attacker already recorded: the path's last
    /// translation is the landing hex, the approach facing is kept and an occupied hex is resolved
    /// through the shared domino effect. A DFA has no side-hex fallback, so a blocked chain leaves
    /// the overlap unresolved for the caller to treat as no legal landing (Total Warfare, p. 150).
    /// </summary>
    public DfaLandingResult? Resolve(IUnit attacker)
    {
        if (_game.BattleMap is not { } map) return null;
        if (attacker.MovementTaken is not { } movement) return null;
        if (movement.GetDfaLandingPosition(map) is not { } landing) return null;

        // The forced-displacement direction of an occupied landing hex is measured from the hex
        // where the jump started, not from the airborne approach hex (Total Warfare, p. 150).
        return _dfaDisplacementResolver.ResolveLanding(
            attacker,
            landing,
            movement.Start.Coordinates,
            map,
            _displacement.AllUnits(),
            _game.Id);
    }

    /// <summary>
    /// Grounds the attacker by completing its jump. The landing command carries no position: the
    /// unit's recorded movement path is the authoritative source for it.
    /// </summary>
    public DfaLandingResult? Execute(
        IUnit attacker,
        DfaLandingKind kind = DfaLandingKind.MissLanding) =>
        Apply(attacker, Resolve(attacker), kind);

    public DfaLandingResult? Apply(
        IUnit attacker,
        DfaLandingResult? landing,
        DfaLandingKind kind)
    {
        if (landing is null) return null;

        _displacement.ExecuteDominoCommands(landing.DominoEffect);

        if (landing.IsOverlapUnresolved)
        {
            // The caller resolves an impossible landing as the attacker's destruction before
            // publishing when it can know the outcome. Defensively leave it in place otherwise
            // rather than stacking it on the occupant.
            _game.Logger.LogDfaAttackerCouldNotResolveLandingHex(attacker.Name, landing.Destination.Coordinates);
            return landing;
        }

        _displacement.Execute(new DfaLandingAction(
            new DfaLandingCommand
            {
                UnitId = attacker.Id,
                LandingKind = kind,
                GameOriginId = _game.Id,
                Timestamp = DateTime.UtcNow
            },
            publish: true));
        return landing;
    }

    /// <summary>
    /// The miss fall: two levels, every hit location rear, no initial piloting skill roll
    /// (Total Warfare, p. 150).
    /// </summary>
    public void ApplyFall(IUnit attacker)
    {
        if (attacker is not Mech attackerMech) return;
        if (attackerMech.Position is not { } position) return;

        var facingRoll = new DiceResult(_game.RulesProvider.GetDfaFallFacingRoll());
        var facingAfterFall = _game.RulesProvider.GetFacingAfterFall(
            facingRoll.Result,
            position.Facing);
        var fallContext = _game.FallProcessor.ProcessMovementAttempt(
            attackerMech,
            new DfaFallRollContext(
                _game.RulesProvider.GetDfaFallLevels(),
                facingRoll,
                facingAfterFall),
            _game,
            MovementType.Jump,
            position);

        if (fallContext.IsFalling)
            _displacement.Execute(new ApplyFallAction(attackerMech, fallContext.ToMechFallCommand()));
    }

    /// <summary>
    /// Adds the water-entry modifier to the attacker's +4 landing PSR when it landed in water.
    /// Entering water is not a separate roll (Total Warfare, p. 150).
    /// </summary>
    public void ApplyWaterEntryModifier(IUnit attacker, IBattleMap map)
    {
        if (!_state.AccumulatedDamage.TryGetValue(attacker.Id, out var accumulated)) return;

        var index = accumulated.RollContexts.FindIndex(
            context => context is DfaAttackerImpactRollContext);
        if (index < 0) return;

        if (attacker.Position is not { Surface: HexSurface.Ground } position) return;

        var waterDepth = map.GetHex(position.Coordinates)?.GetWaterDepth() ?? 0;
        if (waterDepth < 1) return;

        var context = (DfaAttackerImpactRollContext)accumulated.RollContexts[index];
        accumulated.RollContexts[index] = context with { WaterDepth = waterDepth };
    }
}
