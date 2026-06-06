using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;

public class BridgeCollapsedAction(BridgeCollapsedCommand command, bool publish = true) : IGameAction
{
    public void Execute(ServerGame game, IList<IGameCommand> commands)
    {
        game.OnBridgeCollapsed(command);
        if (publish)
        {
            commands.Add(command);
        }
    }
}
