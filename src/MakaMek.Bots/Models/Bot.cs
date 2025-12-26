using System.Reactive.Linq;
using System.Reactive.Concurrency;
using AsyncAwaitBestPractices;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Bots.Services;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;

namespace Sanet.MakaMek.Bots.Models;

/// <summary>
/// Represents a bot player that observes game state and makes automated decisions
/// </summary>
public class Bot : IBot
{
    private readonly IDecisionEngineProvider _decisionEngineProvider;
    private readonly IClientGame _clientGame;
    private IBotDecisionEngine? _currentDecisionEngine;
    private IDisposable? _phaseStateSubscription;
    private IDisposable? _phaseSubscription;
    private IDisposable? _gameEndSubscription;
    private bool _isDisposed;
    private readonly Lock _lock = new();

    public Guid PlayerId { get; }

    public Bot(
        Guid playerId,
        IClientGame clientGame,
        IDecisionEngineProvider decisionEngineProvider)
    {
        PlayerId = playerId;
        _clientGame = clientGame;
        _decisionEngineProvider = decisionEngineProvider;
        
        // Subscribe to phase changes to update the decision engine
        _phaseSubscription = clientGame.PhaseChanges
            .Subscribe(OnPhaseChanged);

        // Subscribe to active player changes (works for both server-driven and client-driven phases)
        _phaseStateSubscription = clientGame.PhaseStepChanges
            .Subscribe(OnPhaseStateChanged);
        
        // Subscribe to game end events
        _gameEndSubscription = clientGame.Commands
            .OfType<GameEndedCommand>()
            .Subscribe(_ => Dispose());
    }

    private void OnPhaseStateChanged(PhaseStepState? phaseStepState)
    {
        lock (_lock)
        {
            if (_isDisposed) return;
        }

        // Check if this bot is now the active player
        if (phaseStepState?.ActivePlayer.Id == PlayerId)
        {
            Task.Run(MakeDecision);
        }
    }

    private void OnPhaseChanged(PhaseNames phase)
    {
        lock (_lock)
        {
            if (_isDisposed) return;
            UpdateDecisionEngine(phase);
        }
    }

    private void UpdateDecisionEngine(PhaseNames phase)
    {
        _currentDecisionEngine = _decisionEngineProvider.GetEngineForPhase(phase);
    }

    private async Task MakeDecision()
    {
        IBotDecisionEngine? engine;
        lock (_lock)
        {
            if (_isDisposed) return;
            engine = _currentDecisionEngine;
        }

        if (engine == null) return;

        // Get the current Player instance from the game's Players collection
        var player = _clientGame.Players.FirstOrDefault(p => p.Id == PlayerId);
        if (player == null)
        {
            Console.WriteLine($"Bot with PlayerId {PlayerId} not found in game players");
            return;
        }

        await Task.Delay(500); // Make more human-like
        await engine.MakeDecision(player);
    }

    public IBotDecisionEngine? DecisionEngine
    {
        get
        {
            lock (_lock)
            {
                return _currentDecisionEngine;
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed) return;
            _isDisposed = true;
        }

        _phaseStateSubscription?.Dispose();
        _phaseStateSubscription = null;

        _phaseSubscription?.Dispose();
        _phaseSubscription = null;

        _gameEndSubscription?.Dispose();
        _gameEndSubscription = null;
    }
}

