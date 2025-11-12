using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Bots.DecisionEngines;

/// <summary>
/// Decision engine for the end phase
/// </summary>
public class EndPhaseEngine : IBotDecisionEngine
{
    private readonly ClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly BotDifficulty _difficulty;

    public EndPhaseEngine(ClientGame clientGame, IPlayer player, BotDifficulty difficulty)
    {
        _clientGame = clientGame;
        _player = player;
        _difficulty = difficulty;
    }

    public Task MakeDecision()
    {
        // TODO: Implement end phase logic
        // 1. Check for shutdown units (always attempt restart)
        // 2. Check for overheated units (shutdown if heat > 25)
        // 3. Publish EndTurnCommand
        // 4. Publish ShutdownUnitCommand when overheated
        // 5. Publish StartupUnitCommand when shutdown
        
        return Task.CompletedTask;
    }
}

