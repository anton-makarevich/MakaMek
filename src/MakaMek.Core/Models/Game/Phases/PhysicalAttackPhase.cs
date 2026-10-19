using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public class PhysicalAttackPhase(ServerGame game) : MainGamePhase(game)
{
    public override void Enter()
    {
        // Temporary auto-advance: no client can declare or pass a physical attack yet, so the
        // phase would stall waiting for a PhysicalAttackCommand. Remove this override once
        // declaration, pass, UI, and bot support are available.
        Game.TransitionToNextPhase(Name);
    }

    public override void HandleCommand(IGameCommand command)
    {
        if (command is not PhysicalAttackCommand attackCommand) return;
        HandleUnitAction(command, attackCommand.PlayerId);
    }

    protected override void ProcessCommand(IGameCommand command)
    {
        var attackCommand = (PhysicalAttackCommand)command;
        var broadcastCommand = attackCommand;
        broadcastCommand.GameOriginId = Game.Id;
        Game.OnPhysicalAttack(attackCommand);
        Game.CommandPublisher.PublishCommand(broadcastCommand);
    }

    public override PhaseNames Name => PhaseNames.PhysicalAttack;
}
