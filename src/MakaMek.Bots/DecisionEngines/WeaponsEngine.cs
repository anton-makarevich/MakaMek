using Sanet.MakaMek.Bots.Models;
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
    private readonly IClientGame _clientGame;
    private readonly BotDifficulty _difficulty;

    public WeaponsEngine(IClientGame clientGame, BotDifficulty difficulty)
    {
        _clientGame = clientGame;
        _difficulty = difficulty;
    }

    public async Task MakeDecision(IPlayer player)
    {
        try
        {
            // 1. Find units that haven't attacked
            var unitsToAttack = player.AliveUnits.Where(u => !u.HasDeclaredWeaponAttack).ToList();
            if (!unitsToAttack.Any())
            {
                // No units to attack with, skip turn
                return;
            }

            // 2. Select first unit that can attack (simple strategy)
            var attackingUnit = unitsToAttack.First();

            if (attackingUnit.Position == null || !attackingUnit.CanFireWeapons)
            {
                // Unit not deployed or can't fire weapons
                return;
            }

            // 3. Find potential targets
            var potentialTargets = GetPotentialTargets(player);
            if (!potentialTargets.Any())
            {
                // No targets available, skip attack
                return;
            }

            // 4. Select random target
            var target = potentialTargets[Random.Shared.Next(potentialTargets.Count)];

            // 5. Get weapons that can reach the target
            var weaponsInRange = GetWeaponsInRange(attackingUnit, target);
            if (!weaponsInRange.Any())
            {
                // No weapons in range, skip attack
                return;
            }

            // 6. Create weapon target data for all weapons in range
            var weaponTargets = weaponsInRange.Select(weapon => new WeaponTargetData
            {
                Weapon = weapon.ToData(),
                TargetId = target.Id,
                IsPrimaryTarget = true
            }).ToList();

            // 7. Declare weapon attack
            var attackCommand = new WeaponAttackDeclarationCommand
            {
                GameOriginId = _clientGame.Id,
                PlayerId = player.Id,
                UnitId = attackingUnit.Id,
                WeaponTargets = weaponTargets
            };

            await _clientGame.DeclareWeaponAttack(attackCommand);
        }
        catch (Exception ex)
        {
            // Log error but don't throw - graceful degradation
            Console.WriteLine($"WeaponsEngine error for player {player.Name}: {ex.Message}");
        }
    }

    private List<IUnit> GetPotentialTargets(IPlayer player)
    {
        // Find enemy units that are deployed and alive
        return _clientGame.Players
            .Where(p => p.Id != player.Id)
            .SelectMany(p => p.AliveUnits)
            .Where(u => u.Position != null)
            .ToList();
    }

    private List<Weapon> GetWeaponsInRange(IUnit attackingUnit, IUnit target)
    {
        if (attackingUnit.Position == null || target.Position == null)
            return [];

        var distance = attackingUnit.Position.Coordinates.DistanceTo(target.Position.Coordinates);

        // Get all functional weapons that can reach the target
        return attackingUnit.GetAvailableComponents<Weapon>()
            .Where(weapon => distance >= weapon.MinimumRange && distance <= weapon.LongRange)
            .ToList();
    }
}

