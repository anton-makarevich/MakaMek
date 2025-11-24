using AsyncAwaitBestPractices;
using Sanet.MakaMek.Bots.DecisionEngines;
using Sanet.MakaMek.Bots.Services;
using Sanet.MakaMek.Core.Data.Game.Commands;
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
    private IBotDecisionEngine? _currentDecisionEngine;
    private IDisposable? _commandSubscription;
    private bool _isDisposed;

    public IPlayer Player { get; }

    public Bot(
        IPlayer player,
        IClientGame clientGame,
        IDecisionEngineProvider decisionEngineProvider)
    {
        Player = player;
        _decisionEngineProvider = decisionEngineProvider;

        // Subscribe to game commands
        _commandSubscription = clientGame.Commands.Subscribe(OnCommandReceived);
    }

    private void OnCommandReceived(IGameCommand command)
    {
        if (_isDisposed) return;

        switch (command)
        {
            case ChangeActivePlayerCommand activePlayerCmd:
                if (activePlayerCmd.PlayerId == Player.Id)
                {
                    MakeDecisionAsync().SafeFireAndForget();
                }
                break;

            case ChangePhaseCommand phaseCmd:
                UpdateDecisionEngine(phaseCmd.Phase);
                break;

            case GameEndedCommand:
                Dispose();
                break;
        }
    }

    private void UpdateDecisionEngine(PhaseNames phase)
    {
        _currentDecisionEngine = _decisionEngineProvider.GetEngineForPhase(phase);
    }

    private async Task MakeDecisionAsync()
    {
        if (_currentDecisionEngine == null) return;

        try
        {
            await _currentDecisionEngine.MakeDecision(Player);
        }
        catch (Exception ex)
        {
            // Graceful degradation: log error but don't break the game
            Console.WriteLine($"Bot {Player.Name} decision error: {ex.Message}");
            // TODO: Consider taking a safe default action to prevent game stuck 
        }
    }
    
    public IBotDecisionEngine? DecisionEngine => _currentDecisionEngine;

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _commandSubscription?.Dispose();
        _commandSubscription = null;
    }
}

