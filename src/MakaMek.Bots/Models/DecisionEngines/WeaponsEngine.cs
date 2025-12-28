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
    private readonly ITacticalEvaluator _tacticalEvaluator;

    public WeaponsEngine(IClientGame clientGame, ITacticalEvaluator tacticalEvaluator)
    {
        _clientGame = clientGame;
        _tacticalEvaluator = tacticalEvaluator;
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

            // Find potential targets using tactical evaluator
             var enemies = _clientGame.Players
                .Where(p => p.Id != player.Id)
                .SelectMany(p => p.AliveUnits)
                .Where(u => u is { IsDeployed: true })
                .ToList();

            var targetScores = _tacticalEvaluator.EvaluateTargets(unitToAttack, enemies);
            
            if (targetScores.Count == 0)
            {
                // No valid targets (no hit probability > 0), declare attack with empty weapon list
                await DeclareWeaponAttack(player, unitToAttack, []);
                return;
            }

            // Select best target
            var bestTargetScore = targetScores.MaxBy(t => t.Score);
            var target = enemies.First(e => e.Id == bestTargetScore.TargetId);
            
            Console.WriteLine($"[WeaponsEngine] Selected target {target.Name} with score {bestTargetScore.Score:F1}");

            // Filter weapons that can actually hit reliably (threshold > 30%)
            const double hitProbabilityThreshold = 0.3;
            var weaponsToFire = bestTargetScore.ViableWeapons
                .Where(w => w.HitProbability >= hitProbabilityThreshold)
                .ToList();

            if (weaponsToFire.Count == 0)
            {
                // No weapons hit the threshold, declare empty attack
                await DeclareWeaponAttack(player, unitToAttack, []);
                return;
            }

            // Create weapon target data for selected weapons
            var weaponTargets = weaponsToFire.Select(evaluation => new WeaponTargetData
            {
                Weapon = evaluation.Weapon.ToData(),
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

