using Sanet.MakaMek.Core.Data.Game.Commands;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;

public interface IGameAction
{
    /// <summary>
    /// Applies the state mutation and optionally appends generated commands
    /// (e.g., critical hits, consciousness rolls) to the commands' list.
    /// </summary>
    void Execute(ServerGame game, IList<IGameCommand> commands);
}
