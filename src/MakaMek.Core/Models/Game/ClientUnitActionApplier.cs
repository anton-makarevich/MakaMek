using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;

namespace Sanet.MakaMek.Core.Models.Game;

/// <summary>
/// Applies the client commands that mutate units during their own turn (deployment, movement,
/// weapon configuration and attack declarations).
/// </summary>
internal sealed class ClientUnitActionApplier
{
    private readonly ClientGame _game;

    public ClientUnitActionApplier(ClientGame game)
    {
        _game = game;
    }

    public bool TryApply(IGameCommand command)
    {
        switch (command)
        {
            case DeployUnitCommand deployUnitCommand:
                _game.OnDeployUnit(deployUnitCommand);
                return true;
            case MoveUnitCommand moveUnitCommand:
                _game.OnMoveUnit(moveUnitCommand);
                return true;
            case WeaponConfigurationCommand weaponConfigurationCommand:
                _game.OnWeaponConfiguration(weaponConfigurationCommand);
                return true;
            case WeaponAttackDeclarationCommand weaponAttackDeclarationCommand:
                _game.OnWeaponsAttack(weaponAttackDeclarationCommand);
                return true;
            case PhysicalAttackCommand physicalAttackCommand:
                _game.OnPhysicalAttack(physicalAttackCommand);
                return true;
            default:
                return false;
        }
    }
}
