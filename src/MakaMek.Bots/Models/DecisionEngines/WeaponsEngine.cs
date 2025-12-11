using Sanet.MakaMek.Bots.Exceptions;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Bots.Models.DecisionEngines;

/// <summary>
/// Decision engine for the weapons phase
/// </summary>
public class WeaponsEngine : IBotDecisionEngine
{
    private readonly IClientGame _clientGame;

    public WeaponsEngine(IClientGame clientGame)
    {
        _clientGame = clientGame;
    }

    public async Task MakeDecision(IPlayer player)
    {
        try
        {
            // Find units that haven't attacked and can fire weapons
            var unitToAttack = player.AliveUnits
                .FirstOrDefault(u => u is { HasDeclaredWeaponAttack: false, CanFireWeapons: true });

            if (unitToAttack == null)
            {
                // No units to attack with, skip turn
                await SkipTurn(player);
                return;
            }

            // Find potential targets
            var potentialTargets = GetPotentialTargets(player);
            if (potentialTargets.Count == 0)
            {
                // No targets available, declare attack with empty weapon list
                await DeclareWeaponAttack(player, unitToAttack, []);
                return;
            }

            // Select random target
            var target = potentialTargets[Random.Shared.Next(potentialTargets.Count)];

            // Get weapons in range of the target
            var weaponsInRange = GetWeaponsInRange(unitToAttack, target);
            if (weaponsInRange.Count == 0)
            {
                // No weapons in range, declare attack with empty weapon list
                await DeclareWeaponAttack(player, unitToAttack, []);
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
            await DeclareWeaponAttack(player, unitToAttack, weaponTargets);
        }
        catch (BotDecisionException ex)
        {
            // Rethrow BotDecisionException to let caller handle decision failures
            Console.WriteLine($"WeaponsEngine error for player {player.Name}: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            // Log error but don't throw - graceful degradation
            Console.WriteLine($"WeaponsEngine error for player {player.Name}: {ex.Message}");
            // If anything fails, skip turn to avoid blocking the game
            await SkipTurn(player);
        }
    }

    private List<IUnit> GetPotentialTargets(IPlayer player)
    {
        if (_clientGame.BattleMap == null)
            return [];

        // Get all enemy units that are deployed and alive
        var enemies = _clientGame.Players
            .Where(p => p.Id != player.Id)
            .SelectMany(p => p.AliveUnits)
            .Where(u => u is { IsDeployed: true, Position: not null })
            .ToList();

        // Filter by line of sight - only check if we have units that can attack
        var attackers = player.AliveUnits
            .Where(u => u is { HasDeclaredWeaponAttack: false, CanFireWeapons: true, Position: not null })
            .ToList();

        if (attackers.Count == 0)
            return [];

        // For simplicity, use the first attacker's position for LOS check
        var attackerPosition = attackers[0].Position!.Coordinates;

        return enemies
            .Where(enemy => _clientGame.BattleMap.HasLineOfSight(
                attackerPosition,
                enemy.Position!.Coordinates))
            .ToList();
    }

    private List<Weapon> GetWeaponsInRange(IUnit attackingUnit, IUnit target)
    {
        if (attackingUnit.Position == null || target.Position == null)
            return [];

        // Calculate distance once and cache it
        var distance = attackingUnit.Position.Coordinates.DistanceTo(target.Position.Coordinates);

        // Get all available weapons that are in range
        return attackingUnit.GetAvailableComponents<Weapon>()
            .Where(weapon => distance >= weapon.MinimumRange && distance <= weapon.LongRange)
            .ToList();
    }

    private async Task DeclareWeaponAttack(IPlayer player, IUnit unit, List<WeaponTargetData> weaponTargets)
    {
        var command = new WeaponAttackDeclarationCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = player.Id,
            UnitId = unit.Id,
            WeaponTargets = weaponTargets
        };

        await _clientGame.DeclareWeaponAttack(command);
    }

    private async Task SkipTurn(IPlayer player)
    {
        // Find any unit that hasn't declared attack yet
        var unit = player.AliveUnits.FirstOrDefault(u => !u.HasDeclaredWeaponAttack);
        if (unit == null)
        {
            throw new BotDecisionException(
                $"No units available for player {player.Name}",
                nameof(WeaponsEngine),
                player.Id);
        }
        // Send weapon attack declaration with empty weapon list
        await DeclareWeaponAttack(player, unit, []);
    }
}

