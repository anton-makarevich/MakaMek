using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Bots.Services;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
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
    private readonly int _decisionDelayMilliseconds;
    private readonly IClientGame _clientGame;
    private ITurnState? _currentTurnState;
    private IDisposable? _phaseStateSubscription;
    private IDisposable? _phaseSubscription;
    private IDisposable? _gameCommandsSubscription;
    private bool _isDisposed;
    private readonly CancellationTokenSource _cts = new();

    public Guid PlayerId { get; }

    public Bot(
        Guid playerId,
        IClientGame clientGame,
        IDecisionEngineProvider decisionEngineProvider,
        int decisionDelayMilliseconds = 1000)
    {
        PlayerId = playerId;
        _clientGame = clientGame;
        _decisionEngineProvider = decisionEngineProvider;
        _decisionDelayMilliseconds = decisionDelayMilliseconds;

        // Subscribe to phase changes to update the decision engine
        _phaseSubscription = clientGame.PhaseChanges
            .Subscribe(OnPhaseChanged);

        // Subscribe to active player changes (works for both server-driven and client-driven phases)
        _phaseStateSubscription = clientGame.PhaseStepChanges
            .Subscribe(OnPhaseStateChanged);
        
        // Subscribe to all relevant commands
        _gameCommandsSubscription = clientGame.Commands
            .Subscribe(HandleGameCommand);
    }

    private void HandleGameCommand(object command)
    {
        if (_isDisposed) return;
        
        switch (command)
        {
            case GameEndedCommand:
                Dispose();
                break;
            case TurnIncrementedCommand turnCmd:
                // Reset state on a new turn
                _currentTurnState = new TurnState(_clientGame.Id, turnCmd.TurnNumber);
                break;
            case WeaponConfigurationCommand weaponConfig:
                if (_clientGame.TurnPhase == PhaseNames.WeaponsAttack
                    && weaponConfig.PlayerId == PlayerId 
                    && _clientGame.PhaseStepState?.ActivePlayer.Id == PlayerId)
                {
                    SetStateActiveUnitId(weaponConfig.UnitId);
                    Task.Run(MakeDecision, _cts.Token);
                }
                break;
            case MechStandUpCommand standUpCommand:
                // After standup, we need to decide what to do next
                ContinueAfterStandUpOrFall(standUpCommand.UnitId);
                break;
            case MechFallCommand fallCommand:    
                // After (failed) standup, we need to decide what to do next
                ContinueAfterStandUpOrFall(fallCommand.UnitId);
                break;
        }
    }

    private void ContinueAfterStandUpOrFall(Guid unitId)
    {
        if (_clientGame.TurnPhase != PhaseNames.Movement
            || _clientGame.PhaseStepState?.ActivePlayer.Id != PlayerId
            || _clientGame.PhaseStepState?.ActivePlayer.Units.Any(u => u.Id == unitId) != true) return;
        SetStateActiveUnitId(unitId);
        Task.Run(MakeDecision, _cts.Token);
    }

    private void SetStateActiveUnitId(Guid? unitId)
    {
        _currentTurnState ??= new TurnState(_clientGame.Id, _clientGame.Turn);
        _currentTurnState?.PhaseActiveUnitId = unitId;
    }

    private void OnPhaseStateChanged(PhaseStepState? phaseStepState)
    {
        if (_isDisposed) return;

        // Check if this bot is now the active player and the phase matches
        if (_clientGame.TurnPhase == phaseStepState?.Phase
            && phaseStepState.Value.ActivePlayer.Id == PlayerId)
        {
            SetStateActiveUnitId(null);
            Task.Run(MakeDecision, _cts.Token);
        }
    }

    private void OnPhaseChanged(PhaseNames phase)
    {
        if (_isDisposed) return;
        UpdateDecisionEngine(phase);
    }

    private void UpdateDecisionEngine(PhaseNames phase)
    {
        DecisionEngine = _decisionEngineProvider.GetEngineForPhase(phase);
    }

    private async Task MakeDecision()
    {
        if (_isDisposed) return;
            
        if (DecisionEngine == null) return;

        // Get the current Player instance from the game's Players collection
        var player = _clientGame.Players.FirstOrDefault(p => p.Id == PlayerId);
        if (player == null)
        {
            _clientGame.Logger.LogWarning("Bot with PlayerId {PlayerId} not found in game players", PlayerId);
            return;
        }
        
        await Task.Delay(_decisionDelayMilliseconds); // Make more human-like
        await DecisionEngine.MakeDecision(player, _currentTurnState);
    }

    public IBotDecisionEngine? DecisionEngine { get; private set; }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        _cts.Cancel();
        _cts.Dispose();

        _phaseStateSubscription?.Dispose();
        _phaseStateSubscription = null;

        _phaseSubscription?.Dispose();
        _phaseSubscription = null;

        _gameCommandsSubscription?.Dispose();
        _gameCommandsSubscription = null;
        
        GC.SuppressFinalize(this);
    }
}

