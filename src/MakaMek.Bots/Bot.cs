using Sanet.MakaMek.Bots.DecisionEngines;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Bots;

/// <summary>
/// Represents a bot player that observes game state and makes automated decisions
/// </summary>
public class Bot : IBot
{
    private readonly Dictionary<PhaseNames, IBotDecisionEngine> _decisionEngines;
    private IBotDecisionEngine? _currentDecisionEngine;
    private IDisposable? _commandSubscription;
    private bool _isDisposed;

    public IPlayer Player { get; }
    public BotDifficulty Difficulty { get; }

    public Bot(
        IPlayer player,
        ClientGame clientGame,
        BotDifficulty difficulty)
    {
        Player = player;
        Difficulty = difficulty;

        // Initialize decision engines for each phase
        _decisionEngines = new Dictionary<PhaseNames, IBotDecisionEngine>
        {
            { PhaseNames.Deployment, new DeploymentEngine(clientGame, player, difficulty) },
            { PhaseNames.Movement, new MovementEngine(clientGame, player, difficulty) },
            { PhaseNames.WeaponsAttack, new WeaponsEngine(clientGame, player, difficulty) },
            { PhaseNames.End, new EndPhaseEngine(clientGame, player, difficulty) }
        };

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
                    _ = MakeDecisionAsync();
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
        _currentDecisionEngine = _decisionEngines.GetValueOrDefault(phase);
    }

    private async Task MakeDecisionAsync()
    {
        if (_currentDecisionEngine == null) return;

        try
        {
            // Optional: Add thinking delay to make bot feel more natural
            var thinkingDelay = GetThinkingDelay();
            if (thinkingDelay > 0)
            {
                await Task.Delay(thinkingDelay);
            }

            await _currentDecisionEngine.MakeDecision();
        }
        catch (Exception ex)
        {
            // Graceful degradation: log error but don't break the game
            Console.WriteLine($"Bot {Player.Name} decision error: {ex.Message}");
            // TODO: Consider taking a safe default action to prevent game stuck 
        }
    }

    private int GetThinkingDelay()
    {
        // Add a small delay to make bot decisions feel more natural
        // Adjust based on difficulty level or randomly
        return 0;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _commandSubscription?.Dispose();
        _commandSubscription = null;
    }
}

