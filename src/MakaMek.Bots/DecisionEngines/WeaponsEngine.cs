using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Bots.DecisionEngines;

/// <summary>
/// Decision engine for the weapons phase
/// </summary>
public class WeaponsEngine : IBotDecisionEngine
{
    private readonly ClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly BotDifficulty _difficulty;

    public WeaponsEngine(ClientGame clientGame, IPlayer player, BotDifficulty difficulty)
    {
        _clientGame = clientGame;
        _player = player;
        _difficulty = difficulty;
    }

    public Task MakeDecision()
    {
        // TODO: Implement weapons logic
        // 1. Find units that haven't attacked: player.AliveUnits.Where(u => !u.HasDeclaredWeaponAttack)
        // 2. Find targets in range
        // 3. Select random target
        // 4. Select all weapons in range
        // 5. Publish WeaponAttackDeclarationCommand and optionally WeaponConfigurationCommand
        
        return Task.CompletedTask;
    }
}

