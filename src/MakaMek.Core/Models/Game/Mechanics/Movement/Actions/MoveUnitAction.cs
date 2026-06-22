using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;

public class MoveUnitAction(MoveUnitCommand command, bool publish = false) : IGameAction
{
    public MoveUnitCommand Command => command;

    public IReadOnlyList<IGameCommand> Process(ServerGame game)
    {
        game.OnMoveUnit(command);
        return publish ? new IGameCommand[] { command } : [];
    }
}
