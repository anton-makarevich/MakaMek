using System.Reactive.Linq;
using System.Reactive.Concurrency;
using AsyncAwaitBestPractices;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Bots.Services;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Bots.Models;

/// <summary>
/// Represents a bot player that observes game state and makes automated decisions
/// </summary>
public class Bot : IBot
{
    private readonly IDecisionEngineProvider _decisionEngineProvider;
    private readonly IClientGame _clientGame;
    private IBotDecisionEngine? _currentDecisionEngine;
    private IDisposable? _activePlayerSubscription;
    private IDisposable? _phaseSubscription;
    private IDisposable? _gameEndSubscription;
    private bool _isDisposed;

    public Guid PlayerId { get; }

    public Bot(
        Guid playerId,
        IClientGame clientGame,
        IDecisionEngineProvider decisionEngineProvider,
        IScheduler? scheduler = null)
    {
        PlayerId = playerId;
        _clientGame = clientGame;
        _decisionEngineProvider = decisionEngineProvider;
        var schedulerToUse = scheduler ?? TaskPoolScheduler.Default;

        // Subscribe to active player changes (works for both server-driven and client-driven phases)
        _activePlayerSubscription = clientGame.ActivePlayerChanges
            .ObserveOn(schedulerToUse)
            .Subscribe(OnActivePlayerChanged);

        // Subscribe to phase changes to update the decision engine
        _phaseSubscription = clientGame.PhaseChanges
            .ObserveOn(schedulerToUse)
            .Subscribe(OnPhaseChanged);

        // Subscribe to game end events
        _gameEndSubscription = clientGame.Commands
            .OfType<GameEndedCommand>()
            .ObserveOn(schedulerToUse)
            .Subscribe(_ => Dispose());
    }

    private void OnActivePlayerChanged(IPlayer? activePlayer)
    {
        if (_isDisposed) return;

        // Check if this bot is now the active player
        if (activePlayer?.Id == PlayerId)
        {
            MakeDecision().SafeFireAndForget();
        }
    }

    private void OnPhaseChanged(PhaseNames phase)
    {
        if (_isDisposed) return;
        UpdateDecisionEngine(phase);
    }

    private void UpdateDecisionEngine(PhaseNames phase)
    {
        _currentDecisionEngine = _decisionEngineProvider.GetEngineForPhase(phase);
    }

    private async Task MakeDecision()
    {
        if (_currentDecisionEngine == null) return;

        // Get the current Player instance from the game's Players collection
        var player = _clientGame.Players.FirstOrDefault(p => p.Id == PlayerId);
        if (player == null)
        {
            Console.WriteLine($"Bot with PlayerId {PlayerId} not found in game players");
            return;
        }

        await _currentDecisionEngine.MakeDecision(player);
    }

    public IBotDecisionEngine? DecisionEngine => _currentDecisionEngine;

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _activePlayerSubscription?.Dispose();
        _activePlayerSubscription = null;

        _phaseSubscription?.Dispose();
        _phaseSubscription = null;

        _gameEndSubscription?.Dispose();
        _gameEndSubscription = null;
    }
}

