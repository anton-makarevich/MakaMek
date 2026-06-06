using Sanet.MakaMek.Core.Data.Game.Commands;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;

public class PublishCommandAction(IGameCommand command) : IGameAction
{
    public IReadOnlyList<IGameCommand> Process(ServerGame game)
    {
        return [command];
    }
}
