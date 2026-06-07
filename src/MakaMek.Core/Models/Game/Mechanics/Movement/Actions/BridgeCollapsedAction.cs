using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;

public class BridgeCollapsedAction(BridgeCollapsedCommand command, bool publish = true) : IGameAction
{
    public IReadOnlyList<IGameCommand> Process(ServerGame game)
    {
        game.OnBridgeCollapsed(command);
        return publish ? new IGameCommand[] { command } : [];
    }
}
