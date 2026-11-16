using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;

namespace Sanet.MakaMek.Core.Models.Game;

/// <summary>
/// Entry point for applying a received command on a client: completes the matching pending
/// command first, then dispatches to the state, unit-action or combat applier.
/// </summary>
internal sealed class ClientCommandApplier
{
    private readonly ClientGame _game;
    private readonly ClientGameStateApplier _stateApplier;
    private readonly ClientUnitActionApplier _unitActionApplier;
    private readonly ClientCombatApplier _combatApplier;

    public ClientCommandApplier(ClientGame game)
    {
        _game = game;
        _stateApplier = new ClientGameStateApplier(game, game.MapFactory);
        _unitActionApplier = new ClientUnitActionApplier(game);
        _combatApplier = new ClientCombatApplier(game);
    }

    public void Apply(IGameCommand command)
    {
        // IMPORTANT: Complete pending commands BEFORE processing command-specific logic.
        // This prevents race conditions where ActivePlayer changes trigger bot decision-making
        // before '_pendingCommands' is cleared, causing CanActivePlayerAct to incorrectly return false.
        // When we receive a re-broadcasted command, the server has already processed it successfully,
        // so completing the pending task (removing from _pendingCommands) before applying state changes
        // is semantically correct and ensures proper synchronization.
        if (command is IClientCommand { IdempotencyKey: not null } clientCommand)
        {
            _game.CompletePendingCommand(clientCommand.IdempotencyKey.Value, true);
        }

        if (_stateApplier.TryApply(command)) return;
        if (_unitActionApplier.TryApply(command)) return;
        if (_combatApplier.TryApply(command)) return;

        if (command is ErrorCommand { IdempotencyKey: { } idempotencyKey })
        {
            // Complete the pending task with failure
            _game.CompletePendingCommand(idempotencyKey, false);
        }
    }
}
