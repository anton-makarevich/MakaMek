using Sanet.MekForge.Core.Models.Game.Commands;
using Sanet.MekForge.Core.Models.Game.Commands.Client;

namespace Sanet.MekForge.Core.Models.Game.Phases;

public class MovementPhase(ServerGame game) : MainGamePhase(game)
{
    public override void HandleCommand(IGameCommand command)
    {
        if (command is not MoveUnitCommand moveCommand) return;
        HandleUnitAction(command, moveCommand.PlayerId);
    }

    protected override void ProcessCommand(IGameCommand command)
    {
        var moveCommand = (MoveUnitCommand)command;
        var broadcastCommand = moveCommand;
        broadcastCommand.GameOriginId = Game.Id;
        Game.OnMoveUnit(moveCommand);
        Game.CommandPublisher.PublishCommand(broadcastCommand);
    }

    public override PhaseNames Name => PhaseNames.Movement;
}
