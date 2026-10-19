using Sanet.MakaMek.Core.Data.Game.Commands;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public class PhysicalAttackResolutionPhase(ServerGame game) : GamePhase(game)
{
    public override void Enter()
    {
        base.Enter();

        // TODO(M1): resolve queued physical-attack declarations and end-of-phase PSRs here.
        // No declarations can exist yet, so this fast path is the only case.
        Game.TransitionToNextPhase(Name);
    }

    public override void HandleCommand(IGameCommand command)
    {
        // This phase doesn't process incoming commands, but we need to implement this method
    }

    public override PhaseNames Name => PhaseNames.PhysicalAttackResolution;
}
