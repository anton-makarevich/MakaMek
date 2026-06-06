using Sanet.MakaMek.Core.Data.Game.Commands;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;

public class PublishCommandAction(IGameCommand command) : IGameAction
{
    public void Execute(ServerGame game, IList<IGameCommand> commands)
    {
        commands.Add(command);
    }
}
