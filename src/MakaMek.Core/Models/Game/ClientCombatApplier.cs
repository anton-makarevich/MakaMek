using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;

namespace Sanet.MakaMek.Core.Models.Game;

/// <summary>
/// Applies the server-resolved combat commands a client receives (attack resolutions, falling,
/// displacement, heat, shutdown, explosions and critical hits).
/// </summary>
internal sealed class ClientCombatApplier
{
    private readonly ClientGame _game;

    public ClientCombatApplier(ClientGame game)
    {
        _game = game;
    }

    public bool TryApply(IGameCommand command)
    {
        switch (command)
        {
            case WeaponAttackResolutionCommand attackResolutionCommand:
                _game.OnWeaponsAttackResolution(attackResolutionCommand);
                return true;
            case PhysicalAttackResolutionCommand physicalAttackResolutionCommand:
                _game.OnPhysicalAttackResolution(physicalAttackResolutionCommand);
                return true;
            case MechFallCommand mechFallingCommand:
                _game.OnMechFalling(mechFallingCommand);
                return true;
            case BridgeCollapsedCommand bridgeCollapsedCommand:
                _game.OnBridgeCollapsed(bridgeCollapsedCommand);
                return true;
            case DisplaceUnitCommand displaceUnitCommand:
                _game.OnUnitDisplaced(displaceUnitCommand);
                return true;
            case MechStandUpCommand mechStandedUpCommand:
                _game.OnMechStandUp(mechStandedUpCommand);
                return true;
            case HeatUpdatedCommand heatUpdateCommand:
                _game.OnHeatUpdate(heatUpdateCommand);
                return true;
            case PilotConsciousnessRollCommand consciousnessRollCommand:
                _game.OnPilotConsciousnessRoll(consciousnessRollCommand);
                return true;
            case UnitShutdownCommand shutdownCommand:
                _game.OnUnitShutdown(shutdownCommand);
                return true;
            case UnitStartupCommand restartCommand:
                _game.OnMechRestart(restartCommand);
                return true;
            case AmmoExplosionCommand explosionCommand:
                _game.OnAmmoExplosion(explosionCommand);
                return true;
            case CriticalHitsResolutionCommand criticalHitsCommand:
                _game.OnCriticalHitsResolution(criticalHitsCommand);
                return true;
            case HullBreachCommand hullBreachCommand:
                _game.OnHullBreach(hullBreachCommand);
                return true;
            default:
                return false;
        }
    }
}
