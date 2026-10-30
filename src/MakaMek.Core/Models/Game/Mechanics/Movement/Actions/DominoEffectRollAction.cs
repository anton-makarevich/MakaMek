using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;

/// <summary>
/// Resolves the consequences of a domino effect immediately after the displacement (Total Warfare,
/// p. 151-153).
/// <para>
/// First the automatic falls: a unit displaced into a hex two or more levels lower falls and takes
/// normal falling damage. Then the mandatory piloting skill rolls, rolled once all displacement is
/// applied. Mechs that are destroyed, not deployed or already prone (including the ones that just
/// fell) are skipped, which also prevents a second roll for the same displacement. A failed roll is
/// delegated to <see cref="ApplyFallAction"/> so the fall keeps its usual
/// fall → hull breach → critical hits → consciousness order.
/// </para>
/// </summary>
public class DominoEffectRollAction(
    IReadOnlyList<IUnit> candidates,
    IFallProcessor fallProcessor,
    IReadOnlyList<DominoFall>? falls = null) : IGameAction
{
    public IReadOnlyList<IGameCommand> Process(ServerGame game)
    {
        var commands = new List<IGameCommand>();

        foreach (var fall in falls ?? [])
            ApplyDisplacementFall(fall, game, commands);

        foreach (var candidate in candidates)
        {
            if (candidate is not Mech mech) continue;
            if (mech.IsDestroyed || !mech.IsDeployed || mech.IsProne) continue;

            var movementType = mech.MovementTaken?.MovementType ?? MovementType.StandingStill;
            var fallContext = fallProcessor.ProcessMovementAttempt(
                mech, new DominoEffectRollContext(ResolveWaterDepth(mech, game)), game, movementType);
            var fallCommand = fallContext.ToMechFallCommand();

            if (fallContext.IsFalling)
            {
                commands.AddRange(new ApplyFallAction(mech, fallCommand).Process(game));
            }
            else
            {
                // Mirror the end-of-phase PSR path: applying a non-falling command is a no-op,
                // but the command still reports the successful roll to the clients.
                game.OnMechFalling(fallCommand);
                commands.Add(fallCommand);
            }
        }

        return commands;
    }

    /// <summary>
    /// A unit dropped two or more levels by the chain makes an automatic fall. The fall uses the
    /// same mechanics as a displacement fall in a push (facing roll + falling damage).
    /// </summary>
    private void ApplyDisplacementFall(DominoFall fall, ServerGame game, List<IGameCommand> commands)
    {
        if (fall.Unit is not Mech { IsDestroyed: false, IsDeployed: true } mech) return;
        if (mech.Position is not { } position) return;

        var facingRoll = game.DiceRoller.RollD6();
        var facingAfterFall = game.RulesProvider.GetFacingAfterFall(facingRoll.Result, position.Facing);
        var fallContext = fallProcessor.ProcessMovementAttempt(
            mech,
            new CliffFallRollContext(fall.LevelsFallen, facingRoll, facingAfterFall),
            game,
            MovementType.StandingStill,
            position);

        if (fallContext.IsFalling)
            commands.AddRange(new ApplyFallAction(mech, fallContext.ToMechFallCommand()).Process(game));
    }

    /// <summary>
    /// Depth of the water in the hex the Mech was displaced into. Only a ground-surface position
    /// counts: a unit on a bridge deck falls onto the deck, not into the water below.
    /// </summary>
    private static int ResolveWaterDepth(Mech mech, ServerGame game)
    {
        if (mech.Position is not { Surface: HexSurface.Ground } position) return 0;
        return game.BattleMap?.GetHex(position.Coordinates)?.GetWaterDepth() ?? 0;
    }
}
