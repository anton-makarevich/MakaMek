using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Bots.DecisionEngines;

/// <summary>
/// Decision engine for the movement phase
/// </summary>
public class MovementEngine : IBotDecisionEngine
{
    private readonly ClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly BotDifficulty _difficulty;

    public MovementEngine(ClientGame clientGame, IPlayer player, BotDifficulty difficulty)
    {
        _clientGame = clientGame;
        _player = player;
        _difficulty = difficulty;
    }

    public Task MakeDecision()
    {
        // TODO: Implement movement logic
        // 1. Find unmoved units: player.AliveUnits.Where(u => u.MovementTypeUsed == null)
        // 2. Select random movement type (prefer Walk)
        // 3. Calculate random valid path using BattleMap.FindPath()
        // 4. Publish MoveUnitCommand
        
        return Task.CompletedTask;
    }
}

