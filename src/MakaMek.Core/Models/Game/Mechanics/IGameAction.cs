using Sanet.MakaMek.Core.Data.Game.Commands;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

public interface IGameAction
{
    IReadOnlyList<IGameCommand> Process(ServerGame game);
}
