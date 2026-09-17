using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public class PhysicalAttackPhase(ServerGame game) : MainGamePhase(game)
{
    public override void HandleCommand(IGameCommand command)
    {
        var playerId = command switch
        {
            PhysicalAttackCommand attackCommand => attackCommand.PlayerId,
            PassPhysicalAttackCommand passCommand => passCommand.PlayerId,
            _ => Guid.Empty
        };

        if (playerId == Guid.Empty) return;
        HandleUnitAction(command, playerId);
    }

    protected override void ProcessCommand(IGameCommand command)
    {
        switch (command)
        {
            case PhysicalAttackCommand attackCommand:
                var broadcastAttack = attackCommand;
                broadcastAttack.GameOriginId = Game.Id;
                Game.OnPhysicalAttack(attackCommand);
                Game.CommandPublisher.PublishCommand(broadcastAttack);
                break;
            case PassPhysicalAttackCommand passCommand:
                var broadcastPass = passCommand;
                broadcastPass.GameOriginId = Game.Id;
                Game.CommandPublisher.PublishCommand(broadcastPass);
                break;
        }
    }

    public override PhaseNames Name => PhaseNames.PhysicalAttack;
}
