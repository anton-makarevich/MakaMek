using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

public class JumpDamageInterruptHandler : IMovementInterruptHandler
{
    public MovementInterruptResult? Check(MovementInterruptContext context)
    {
        if (context.MoveCommand.MovementType != MovementType.Jump) return null;
        if (context.Unit is not Mech mech || !mech.IsPsrForJumpRequired()) return null;

        var fallContextData = context.Game.FallProcessor.ProcessMovementAttempt(
            mech, new PilotingSkillRollContext(PilotingSkillRollType.JumpWithDamage),
            context.Game, MovementType.Jump);

        if (!fallContextData.IsFalling)
        {
            var psrCommand = fallContextData.ToMechFallCommand();
            return new MovementInterruptResult
            {
                ShouldStop = false,
                GameActions = new List<IGameAction>
                {
                    new PublishCommandAction(psrCommand)
                }
            };
        }

        var fallCommand = fallContextData.ToMechFallCommand();
        return new MovementInterruptResult
        {
            ShouldStop = true,
            GameActions = new List<IGameAction>
            {
                new ApplyFallAction(mech, fallCommand)
            }
        };
    }
}
