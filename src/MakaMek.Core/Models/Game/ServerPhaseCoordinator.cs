using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Core.Models.Game;

/// <summary>
/// Owns the current server phase and the transitions between phases. Extracted from
/// <see cref="ServerGame"/> so the game class does not have to reference the phase types.
/// </summary>
internal sealed class ServerPhaseCoordinator
{
    private readonly ServerGame _game;
    private readonly IPhaseManager _phaseManager;
    private IGamePhase _currentPhase;

    public ServerPhaseCoordinator(ServerGame game, IPhaseManager? phaseManager)
    {
        _game = game;
        _phaseManager = phaseManager ?? new BattleTechPhaseManager();
        _currentPhase = new StartPhase(game); // Starts in the StartPhase
    }

    public void Dispatch(IGameCommand command)
    {
        _currentPhase.HandleCommand(command);
    }

    public void SetActivePlayer(IPlayer? player, int unitsToMove)
    {
        if (player == null)
        {
            _game.SetPhaseStepState(null);
            return;
        }

        _game.SetPhaseStepState(new PhaseStepState(_game.TurnPhase, player, unitsToMove));
        _game.CommandPublisher.PublishCommand(
            ServerGameCommandFactory.ActivePlayer(_game.Id, player, unitsToMove));
    }

    public void TryStartGame()
    {
        (_currentPhase as StartPhase)?.TryTransitionToNextPhase();
    }

    public void TransitionToNextPhase(PhaseNames currentPhase)
    {
        var nextPhase = _phaseManager.GetNextPhase(currentPhase, _game);
        TransitionToPhase(nextPhase);
    }

    private void TransitionToPhase(IGamePhase newPhase)
    {
        if (_currentPhase is GamePhase currentGamePhase)
        {
            currentGamePhase.Exit();
        }
        _currentPhase = newPhase;
        _game.SetPhase(newPhase.Name);
        _currentPhase.Enter();
    }
}
