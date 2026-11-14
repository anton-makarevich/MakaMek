using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;

/// <summary>
/// Grounds a Death From Above attacker by completing the jump path already recorded on the unit.
/// </summary>
public class DfaLandingAction(DfaLandingCommand command, bool publish = false) : IGameAction
{
    public DfaLandingCommand Command => command;

    public IReadOnlyList<IGameCommand> Process(ServerGame game)
    {
        game.OnDfaLanding(command);
        return publish ? new IGameCommand[] { command } : [];
    }
}
