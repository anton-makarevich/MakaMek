using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;

public class DisplaceUnitAction(DisplaceUnitCommand command, bool publish = false) : IGameAction
{
    public DisplaceUnitCommand Command => command;

    public IReadOnlyList<IGameCommand> Process(ServerGame game)
    {
        game.OnUnitDisplaced(command);
        return publish ? new IGameCommand[] { command } : [];
    }
}
