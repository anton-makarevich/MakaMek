using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;

/// <summary>
/// Resolves the mandatory piloting skill rolls of a domino effect immediately after the
/// displacement that caused them (Total Warfare, p. 151-153).
/// <para>
/// Mechs that are destroyed, not deployed or already prone are skipped: a Mech that fell during the
/// same event is prone, which also prevents a second roll for the same displacement. A failed roll
/// is delegated to <see cref="ApplyFallAction"/> so the fall keeps its usual
/// fall → hull breach → critical hits → consciousness order.
/// </para>
/// </summary>
public class DominoEffectRollAction(IReadOnlyList<IUnit> candidates, IFallProcessor fallProcessor) : IGameAction
{
    public IReadOnlyList<IGameCommand> Process(ServerGame game)
    {
        var commands = new List<IGameCommand>();

        foreach (var candidate in candidates)
        {
            if (candidate is not Mech mech) continue;
            if (mech.IsDestroyed || !mech.IsDeployed || mech.IsProne) continue;

            var movementType = mech.MovementTaken?.MovementType ?? MovementType.StandingStill;
            var fallContext = fallProcessor.ProcessMovementAttempt(
                mech, new DominoEffectRollContext(), game, movementType);
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
}
