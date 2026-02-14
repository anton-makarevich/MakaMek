using System.ComponentModel;
using ModelContextProtocol.Server;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Tools.BotContainer.Models.Data.Mcp;
using Sanet.MakaMek.Tools.BotContainer.Services;

namespace Sanet.MakaMek.Tools.BotContainer.Models.Mcp.Tools;

[McpServerToolType]
public class WeaponsAttackTools
{
    private readonly IGameStateProvider _gameStateProvider;

    public WeaponsAttackTools(IGameStateProvider gameStateProvider)
    {
        _gameStateProvider = gameStateProvider;
    }

    [McpServerTool, Description("Get combat options for a unit, including viable targets with weapon evaluations and required configurations")]
    public async Task<List<TargetOptionData>> GetCombatOptions(Guid unitId)
    {
        if (_gameStateProvider.ClientGame == null)
            throw new InvalidOperationException("Game is not initialized.");
        if (_gameStateProvider.TacticalEvaluator == null)
            throw new InvalidOperationException("TacticalEvaluator is not available.");

        var game = _gameStateProvider.ClientGame;
        
        // Find player and unit
        var player = game.Players.FirstOrDefault(p => p.Units.Any(u => u.Id == unitId));
        var attacker = player?.Units.FirstOrDefault(u => u.Id == unitId);
        
        if (attacker?.Position == null)
            throw new InvalidOperationException($"Unit {unitId} not found or not on map.");

        // Get attacker's movement path (or standing still)
        var attackerPath = attacker.MovementTaken ?? MovementPath.CreateStandingStillPath(attacker.Position);

        // Find enemy units
        var enemyUnits = game.Players
            .Where(p => p.Id != player!.Id)
            .SelectMany(p => p.AliveUnits)
            .Where(u => u is { IsDeployed: true })
            .ToList();

        if (enemyUnits.Count == 0)
        {
            return [];
        }

        // Evaluate targets using TacticalEvaluator
        var targetEvaluations = await _gameStateProvider.TacticalEvaluator.EvaluateTargets(
            attacker, 
            attackerPath, 
            enemyUnits);

        // Convert to DTOs
        var result = new List<TargetOptionData>();
        foreach (var targetEval in targetEvaluations)
        {
            var target = enemyUnits.FirstOrDefault(e => e.Id == targetEval.TargetId);
            if (target == null) continue;

            var configurations = new List<WeaponConfigurationData>();
            foreach (var configScore in targetEval.ConfigurationScores)
            {
                var weapons = new List<WeaponEvaluationData>();
                foreach (var weaponEval in configScore.ViableWeapons)
                {
                    var weapon = weaponEval.Weapon;
                    var remainingShots = weapon.RequiresAmmo 
                        ? attacker.GetRemainingAmmoShots(weapon) 
                        : int.MaxValue;

                    weapons.Add(new WeaponEvaluationData(
                        WeaponName: weapon.Name,
                        Damage: weapon.Damage,
                        Heat: weapon.Heat,
                        MinRange: weapon.MinimumRange,
                        ShortRange: weapon.ShortRange,
                        MediumRange: weapon.MediumRange,
                        LongRange: weapon.LongRange,
                        WeaponType: weapon.Type.ToString(),
                        RequiresAmmo: weapon.RequiresAmmo,
                        RemainingShots: remainingShots,
                        HitProbability: weaponEval.HitProbability
                    ));
                }

                configurations.Add(new WeaponConfigurationData(
                    ConfigurationType: configScore.Configuration.Type.ToString(),
                    Value: configScore.Configuration.Value,
                    Score: configScore.Score,
                    ViableWeapons: weapons
                ));
            }

            result.Add(new TargetOptionData(
                TargetId: target.Id,
                TargetName: target.Name,
                TargetModel: target.Model,
                TargetMass: target.Tonnage,
                CurrentArmor: target.TotalCurrentArmor,
                MaxArmor: target.TotalMaxArmor,
                CurrentStructure: target.TotalCurrentStructure,
                MaxStructure: target.TotalMaxStructure,
                CurrentHeat: target.CurrentHeat,
                IsShutdown: target.IsShutdown,
                Configurations: configurations
            ));
        }

        return result;
    }
}

