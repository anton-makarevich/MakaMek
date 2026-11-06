using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;

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
                if (IsBlockedByMovementAttack(attackCommand))
                {
                    Game.Logger.LogWarning(
                        "Rejecting weapon attack declaration for unit {UnitId}: the unit has a pending movement attack",
                        attackCommand.UnitId);
                    Game.CommandPublisher.PublishCommand(new ErrorCommand
                    {
                        GameOriginId = Game.Id,
                        IdempotencyKey = attackCommand.IdempotencyKey,
                        ErrorCode = ErrorCode.InvalidGameState,
                        Timestamp = DateTime.UtcNow
                    });
                    return;
                }
                HandleUnitAction(command, attackCommand.PlayerId);
                break;
        }
    }

    /// <summary>
    /// A charging or DFA'ing unit cannot fire this turn. An empty declaration is the unit's skip
    /// and is still accepted, so it can complete its weapons step.
    /// </summary>
    private bool IsBlockedByMovementAttack(WeaponAttackDeclarationCommand attackCommand)
    {
        if (attackCommand.WeaponTargets is not { Count: > 0 }) return false;

        var attacker = Game.Players
            .FirstOrDefault(player => player.Id == attackCommand.PlayerId)?
            .Units.FirstOrDefault(unit => unit.Id == attackCommand.UnitId);
        return attacker?.HasPendingMovementAttack == true;
    }

    private void ProcessWeaponConfiguration(WeaponConfigurationCommand configCommand)
    {
        Game.OnWeaponConfiguration(configCommand);
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
