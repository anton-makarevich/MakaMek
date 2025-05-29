using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public class WeaponsAttackPhase(ServerGame game) : MainGamePhase(game)
{
    public override void HandleCommand(IGameCommand command)
    {
        if (command is not IClientCommand clientCommand) return;

        switch (clientCommand)
        {
            case WeaponConfigurationCommand configCommand:
                ProcessWeaponConfiguration(configCommand);
                break;
            case WeaponAttackDeclarationCommand attackCommand:
                HandleUnitAction(command, attackCommand.PlayerId);
                break;
        }
    }

    private void ProcessWeaponConfiguration(WeaponConfigurationCommand configCommand)
    {
        var broadcastConfig = configCommand;
        broadcastConfig.GameOriginId = Game.Id;
        Game.CommandPublisher.PublishCommand(broadcastConfig);
    }

    protected override void ProcessCommand(IGameCommand command)
    {
        var attackCommand = (WeaponAttackDeclarationCommand)command;
        var broadcastAttack = attackCommand;
        broadcastAttack.GameOriginId = Game.Id;
        Game.OnWeaponsAttack(attackCommand);
        Game.CommandPublisher.PublishCommand(broadcastAttack);
    }

    public override PhaseNames Name => PhaseNames.WeaponsAttack;
}
