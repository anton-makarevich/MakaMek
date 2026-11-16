using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Map.Factories;

namespace Sanet.MakaMek.Core.Models.Game;

/// <summary>
/// Applies the game-setup, player and turn/phase commands a client receives. Extracted from
/// <see cref="ClientGame.HandleCommand"/> so the game class keeps only command intake and
/// the pending-command bookkeeping.
/// </summary>
internal sealed class ClientGameStateApplier
{
    private readonly ClientGame _game;
    private readonly IBattleMapFactory _mapFactory;

    public ClientGameStateApplier(ClientGame game, IBattleMapFactory mapFactory)
    {
        _game = game;
        _mapFactory = mapFactory;
    }

    public bool TryApply(IGameCommand command) =>
        TryApplySessionState(command) || TryApplyTurnState(command);

    private bool TryApplySessionState(IGameCommand command)
    {
        switch (command)
        {
            case SetBattleMapCommand setBattleMapCommand:
                // Create a new BattleMap from the received data
                _game.SetBattleMap(_mapFactory.CreateFromData(setBattleMapCommand.MapData));
                return true;
            case JoinGameCommand joinGameCommand:
                _game.OnPlayerJoined(joinGameCommand);
                return true;
            case UpdatePlayerStatusCommand statusCommand:
                _game.OnPlayerStatusUpdated(statusCommand);
                return true;
            default:
                return false;
        }
    }

    private bool TryApplyTurnState(IGameCommand command)
    {
        switch (command)
        {
            case TurnIncrementedCommand turnIncrementedCommand:
                ApplyTurnIncremented(turnIncrementedCommand);
                return true;
            case ChangePhaseCommand phaseCommand:
                ApplyPhaseChange(phaseCommand);
                return true;
            case StartPhaseCommand startPhaseCommand:
                ApplyStartPhase(startPhaseCommand);
                return true;
            case ChangeActivePlayerCommand changeActivePlayerCommand:
                ApplyActivePlayerChange(changeActivePlayerCommand);
                return true;
            case TurnEndedCommand turnEndedCommand:
                ApplyTurnEnded(turnEndedCommand);
                return true;
            default:
                return false;
        }
    }

    private void ApplyTurnIncremented(TurnIncrementedCommand command)
    {
        // Use the validation method from BaseGame
        if (!_game.ValidateTurnIncrementedCommand(command).IsValid) return;

        foreach (var alivePlayer in _game.AlivePlayers)
        {
            _game.OnTurnEnded(alivePlayer.Id);
        }
        _game.SetTurn(command.TurnNumber);
    }

    private void ApplyPhaseChange(ChangePhaseCommand command)
    {
        _game.SetTurnPhase(command.Phase);

        // When entering the End phase, clear the players who ended turn
        // Note: ActivePlayer is set when StartPhaseCommand is received to avoid race condition
        if (command.Phase == PhaseNames.End)
        {
            _game.PlayersEndedTurn.Clear();
        }
    }

    private void ApplyStartPhase(StartPhaseCommand command)
    {
        if (command.Phase != PhaseNames.End) return;

        // When the End phase is fully initialized on the server, set the first alive local player as active
        // This ensures the server has completed phase initialization before bots start making decisions
        var firstLocalPlayer = _game.AlivePlayers.FirstOrDefault(p => _game.IsLocalPlayer(p.Id));
        _game.SetPhaseStepState(firstLocalPlayer != null
            ? new PhaseStepState(_game.TurnPhase, firstLocalPlayer, 0)
            : null);
    }

    private void ApplyActivePlayerChange(ChangeActivePlayerCommand command)
    {
        var player = _game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        _game.SetPhaseStepState(player != null
            ? new PhaseStepState(_game.TurnPhase, player, command.UnitsToPlay)
            : null);
    }

    private void ApplyTurnEnded(TurnEndedCommand command)
    {
        // Record that this player has ended their turn
        _game.PlayersEndedTurn.Add(command.PlayerId);

        // Set the next local player who hasn't ended their turn as active
        var nextActivePlayer = _game.AlivePlayers
            .Where(p => !_game.PlayersEndedTurn.Contains(p.Id))
            .FirstOrDefault(p => _game.IsLocalPlayer(p.Id));

        _game.SetPhaseStepState(nextActivePlayer != null
            ? new PhaseStepState(_game.TurnPhase, nextActivePlayer, 0)
            : null);
    }
}
