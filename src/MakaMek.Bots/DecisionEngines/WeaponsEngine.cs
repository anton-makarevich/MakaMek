using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Bots.DecisionEngines;

/// <summary>
/// Decision engine for the weapons phase
/// </summary>
public class WeaponsEngine : IBotDecisionEngine
{
    private readonly ClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly BotDifficulty _difficulty;
    private readonly Random _random = new();

    public WeaponsEngine(ClientGame clientGame, IPlayer player, BotDifficulty difficulty)
    {
        _clientGame = clientGame;
        _player = player;
        _difficulty = difficulty;
    }

    public async Task MakeDecision()
    {
        try
        {
            // Find units that haven't attacked
            var unitToAttack = _player.AliveUnits
                .FirstOrDefault(u => !u.HasDeclaredWeaponAttack && u.CanFireWeapons);

            if (unitToAttack == null)
            {
                // No units to attack with, skip turn
                await SkipTurn();
                return;
            }

            // Find potential targets
            var potentialTargets = GetPotentialTargets(unitToAttack);
            if (potentialTargets.Count == 0)
            {
                // No targets available, declare attack with empty weapon list
                await DeclareWeaponAttack(unitToAttack, []);
                return;
            }

            // Select random target
            var target = potentialTargets[_random.Next(potentialTargets.Count)];

            // Get weapons in range of the target
            var weaponsInRange = GetWeaponsInRange(unitToAttack, target);
            if (weaponsInRange.Count == 0)
            {
                // No weapons in range, declare attack with empty weapon list
                await DeclareWeaponAttack(unitToAttack, []);
                return;
            }

            // Create weapon target data for all weapons in range
            var weaponTargets = weaponsInRange.Select(weapon => new WeaponTargetData
            {
                Weapon = weapon.ToData(),
                TargetId = target.Id,
                IsPrimaryTarget = true
            }).ToList();

            // Declare weapon attack
            await DeclareWeaponAttack(unitToAttack, weaponTargets);
        }
        catch
        {
            // If anything fails, skip turn to avoid blocking the game
            await SkipTurn();
        }
    }

    private List<Unit> GetPotentialTargets(Unit attacker)
    {
        if (_clientGame.BattleMap == null || attacker.Position == null)
            return [];

        // Get all enemy units that are deployed and alive
        var enemies = _clientGame.Players
            .Where(p => p.Id != _player.Id)
            .SelectMany(p => p.AliveUnits)
            .Where(u => u.IsDeployed && u.Position != null)
            .ToList();

        // Filter by line of sight
        return enemies
            .Where(enemy => _clientGame.BattleMap.HasLineOfSight(
                attacker.Position!.Coordinates,
                enemy.Position!.Coordinates))
            .ToList();
    }

    private List<Weapon> GetWeaponsInRange(Unit attacker, Unit target)
    {
        if (attacker.Position == null || target.Position == null)
            return [];

        var distance = attacker.Position.Coordinates.DistanceTo(target.Position.Coordinates);

        // Get all available weapons that are in range
        return attacker.GetAvailableComponents<Weapon>()
            .Where(w => w.IsInRange(distance))
            .ToList();
    }

    private async Task DeclareWeaponAttack(Unit unit, List<WeaponTargetData> weaponTargets)
    {
        var command = new WeaponAttackDeclarationCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = _player.Id,
            UnitId = unit.Id,
            WeaponTargets = weaponTargets
        };

        await _clientGame.DeclareWeaponAttack(command);
    }

    private async Task SkipTurn()
    {
        // Weapons phase doesn't require explicit turn ending
        // The phase will automatically progress when all units have declared attacks
        await Task.CompletedTask;
    }
}

