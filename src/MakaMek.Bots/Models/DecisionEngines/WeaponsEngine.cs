using Sanet.MakaMek.Bots.Exceptions;
using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Bots.Models.DecisionEngines;

/// <summary>
/// Decision engine for the weapons phase
/// </summary>
public class WeaponsEngine : IBotDecisionEngine
{
    private readonly IClientGame _clientGame;
    private readonly ITacticalEvaluator _tacticalEvaluator;

    // TODO: move all the params like that into config
    // Higher values make the bot more conservative with ammo
    private const double AmmoConservationFactor = 3d;
    
    public WeaponsEngine(IClientGame clientGame, ITacticalEvaluator tacticalEvaluator)
    {
        _clientGame = clientGame;
        _tacticalEvaluator = tacticalEvaluator;
    }

    public async Task MakeDecision(IPlayer player, ITurnState? turnState = null)
    {
        try
        {
            // Find units that haven't attacked and can fire weapons
            var attacker = player.AliveUnits
                .FirstOrDefault(u => u is { HasDeclaredWeaponAttack: false, CanFireWeapons: true });

            if (attacker?.Position == null)
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

            var attackerPath = attacker.MovementTaken ?? MovementPath.CreateStandingStillPath(attacker.Position);
            var targetScores = await _tacticalEvaluator.EvaluateTargets(attacker, attackerPath, enemies, turnState);

            if (targetScores.Count == 0)
            {
                // No valid targets (no hit probability > 0), declare attack with an empty weapon list
                await DeclareWeaponAttack(player, attacker, []);
                return;
            }

            // Find the best configuration across all targets, preserving target context
            var bestOption = targetScores
                .SelectMany(t => t.ConfigurationScores.Select(cs => new 
                { 
                    Target = t, 
                    ConfigScore = cs 
                }))
                .OrderByDescending(x => x.ConfigScore.Score)
                .FirstOrDefault();

            if (bestOption == null || bestOption.ConfigScore.Score == 0)
            {
                // No viable attacks, declare empty attack
                await DeclareWeaponAttack(player, attacker, []);
                return;
            }

            var bestConfig = bestOption.ConfigScore.Configuration;
            var target = enemies.FirstOrDefault(e => e.Id == bestOption.Target.TargetId);
            if (target == null)
            {
                // No valid target found, declare empty attack
                await DeclareWeaponAttack(player, attacker, []);
                return;
            }

            // Check if configuration needs to be applied

            if (bestConfig.Type != WeaponConfigurationType.None)
            {
                if (!attacker.IsWeaponConfigurationApplied(bestConfig))
                {
                    // Send configuration command and END execution
                    Console.WriteLine(
                        $"[WeaponsEngine] Applying {bestConfig.Type} with {(HexDirection)bestConfig.Value} when targeting {target.Name}");
                    await ConfigureWeapons(player, attacker, bestConfig);
                    return;
                }
            }

            Console.WriteLine($"[WeaponsEngine] Selected target {target.Name} with score {bestOption.ConfigScore.Score:F1}");

            // Configuration is applied (or not needed), select weapons and declare attack
            var weaponTargets = SelectWeapons(attacker, bestOption.ConfigScore)
                .Select(evaluation => new WeaponTargetData
            {
                Weapon = evaluation.Weapon.ToData(),
                TargetId = target.Id,
                IsPrimaryTarget = true
            }).ToList();

            if (weaponTargets.Count == 0)
            {
                // No suitable weapons selected, declare empty attack
                await DeclareWeaponAttack(player, attacker, []);
                return;
            }

            // Declare weapon attack
            await DeclareWeaponAttack(player, attacker, weaponTargets);
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
        // Send a weapon attack declaration with an empty weapon list
        await DeclareWeaponAttack(player, unit, []);
    }

    private async Task ConfigureWeapons(IPlayer player, IUnit unit, WeaponConfiguration config)
    {
        var command = new WeaponConfigurationCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = player.Id,
            UnitId = unit.Id,
            Configuration = config
        };

        await _clientGame.ConfigureUnitWeapons(command);
    }

    private List<WeaponEvaluationData> SelectWeapons(IUnit attacker, WeaponConfigurationEvaluationData configEvaluationData)
    {
        var initialProjectedHeat = attacker.GetProjectedHeatValue(_clientGame.RulesProvider);
        var heatDissipation = attacker.HeatDissipation;
        var heatThreshold = GetHeatSelectionThreshold();

        var sortedWeapons = configEvaluationData.ViableWeapons
            .Where(w => w.HitProbability > 0)
            .OrderByDescending(w => w.HitProbability)
            .ThenByDescending(w => w.Weapon.Damage)
            .ToList();

        if (sortedWeapons.Count == 0)
            return [];

        var selectedWeapons = new List<WeaponEvaluationData>();
        var selectedHeat = 0;

        foreach (var evaluation in sortedWeapons)
        {
            var nextSelectedHeat = selectedHeat + evaluation.Weapon.Heat;
            if (initialProjectedHeat + nextSelectedHeat - heatDissipation > heatThreshold)
                continue;

            if (IsFiringWeaponJustified(attacker, evaluation))
            {
                selectedWeapons.Add(evaluation);
                selectedHeat = nextSelectedHeat;
            }
        }

        return selectedWeapons;
    }

    private static bool IsFiringWeaponJustified(IUnit attacker, WeaponEvaluationData evaluation)
    {
        if (!evaluation.Weapon.RequiresAmmo)
            return true;

        var remainingShots = attacker.GetRemainingAmmoShots(evaluation.Weapon);
        if (remainingShots <= 0)
            return false;

        var requiredHitProbability = AmmoConservationFactor / (remainingShots + AmmoConservationFactor);
        requiredHitProbability = Math.Clamp(requiredHitProbability, 0d, 1d);

        return evaluation.HitProbability >= requiredHitProbability;
    }

    private static int GetHeatSelectionThreshold()
    {
        // TODO: Calculate based on risk assessment (hit probability, damage, and heat penalty risks).
        return 5;
    }
}

