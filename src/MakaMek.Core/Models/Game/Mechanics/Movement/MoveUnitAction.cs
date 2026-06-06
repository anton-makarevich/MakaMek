using System.Collections.Generic;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;

public class MoveUnitAction(MoveUnitCommand command, bool publish = false) : IGameAction
{
    public void Execute(ServerGame game, IList<IGameCommand> commands)
    {
        game.OnMoveUnit(command);
        if (publish)
        {
            commands.Add(command);
        }
    }
}
