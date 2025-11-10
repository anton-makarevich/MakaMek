using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Bots.DecisionEngines;

/// <summary>
/// Decision engine for the deployment phase
/// </summary>
public class DeploymentEngine : IBotDecisionEngine
{
    private readonly ClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly BotDifficulty _difficulty;

    public DeploymentEngine(ClientGame clientGame, IPlayer player, BotDifficulty difficulty)
    {
        _clientGame = clientGame;
        _player = player;
        _difficulty = difficulty;
    }

    public Task MakeDecision()
    {
        // TODO: Implement deployment logic
        // 1. Find undeployed units: player.Units.Where(u => !u.IsDeployed)
        // 2. Get valid deployment hexes from map
        // 3. Select random hex and direction
        // 4. Publish DeployUnitCommand
        
        return Task.CompletedTask;
    }
}

