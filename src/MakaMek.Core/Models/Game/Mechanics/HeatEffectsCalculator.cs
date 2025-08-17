using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Utils;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Calculator for heat effects including shutdown and restart mechanics
/// </summary>
public class HeatEffectsCalculator : IHeatEffectsCalculator
{
    private readonly IRulesProvider _rulesProvider;
    private readonly IDiceRoller _diceRoller;
    private readonly ICriticalHitsCalculator _criticalHitsCalculator;

    public HeatEffectsCalculator(IRulesProvider rulesProvider, IDiceRoller diceRoller, ICriticalHitsCalculator criticalHitsCalculator)
    {
        _rulesProvider = rulesProvider;
        _diceRoller = diceRoller;
        _criticalHitsCalculator = criticalHitsCalculator;
    }

    /// <summary>
    /// Gets the shutdown avoid number for a given heat level
    /// </summary>
    /// <param name="heatLevel">The current heat level</param>
    /// <returns>The 2D6 target number needed to avoid shutdown</returns>
    public int GetShutdownAvoidNumber(int heatLevel)
    {
        return _rulesProvider.GetHeatShutdownAvoidNumber(heatLevel);
    }

    public UnitShutdownCommand? CheckForHeatShutdown(Mech mech, int currentTurn)
    {
        // Don't check if already shutdown
        if (mech.IsShutdown)
            return null;

        var currentHeat = mech.CurrentHeat;

        // Get the avoid number based on current heat level
        var avoidNumber = GetShutdownAvoidNumber(currentHeat);
        
        // If avoidNumber is 0, no shutdown check is needed
        if (avoidNumber < DiceUtils.Guaranteed2D6Roll)
            return null;
        
        // Check if pilot is conscious (unconscious pilots automatically fail)
        var isConsciousPilot = mech.Pilot?.IsConscious == true;
        
        // Check for automatic shutdown (avoidNumber 13 means automatic shutdown)
        if (avoidNumber == DiceUtils.Impossible2D6Roll || !isConsciousPilot)
        {
            var automaticShutdownData = new ShutdownData
            {
                Reason = ShutdownReason.Heat,
                Turn = currentTurn
            };
            
            return new UnitShutdownCommand
            {
                UnitId = mech.Id,
                ShutdownData = automaticShutdownData,
                AvoidShutdownRoll = null,
                IsAutomaticShutdown = true,
                GameOriginId = Guid.Empty // Will be set by the calling phase
            };
        }

        // Roll 2D6 to avoid shutdown
        var diceRoll = _diceRoller.Roll2D6();
        var diceResults = diceRoll.Select(d => d.Result).ToArray();
        var rollTotal = diceResults.Sum();
        
        var shutdownOccurs = rollTotal < avoidNumber;
        
        var shutdownData = new ShutdownData
        {
            Reason = ShutdownReason.Heat,
            Turn = currentTurn
        };

        var avoidShutdownRollData = new AvoidShutdownRollData
        {
            HeatLevel = currentHeat,
            DiceResults = diceResults,
            AvoidNumber = avoidNumber,
            IsSuccessful = !shutdownOccurs
        };

        return new UnitShutdownCommand
        {
            UnitId = mech.Id,
            ShutdownData = shutdownData,
            AvoidShutdownRoll = avoidShutdownRollData,
            IsAutomaticShutdown = false,
            GameOriginId = Guid.Empty // Will be set by the calling phase
        };
    }

    public UnitStartupCommand? AttemptRestart(Mech mech, int currentTurn)
    {
        // Must be shutdown to restart
        if (!mech.IsShutdown || !mech.CurrentShutdownData.HasValue)
            return null;

        var shutdownData = mech.CurrentShutdownData.Value;

        // Can't restart in the same turn as shutdown
        if (shutdownData.Turn >= currentTurn)
            return null;

        var currentHeat = mech.CurrentHeat;
        var avoidNumber = GetShutdownAvoidNumber(currentHeat);
        
        // Check for automatic restart due to low heat
        if (avoidNumber < DiceUtils.Guaranteed2D6Roll)
        {
            return new UnitStartupCommand
            {
                UnitId = mech.Id,
                IsAutomaticRestart = true,
                IsRestartPossible = true,
                GameOriginId = Guid.Empty, // Will be set by the calling phase
                AvoidShutdownRoll = null
            };
        }
        
        // Check if pilot is conscious (unconscious pilots automatically fail)
        var isConsciousPilot = mech.Pilot?.IsConscious == true;

        if (avoidNumber == DiceUtils.Impossible2D6Roll || !isConsciousPilot)
        {
            return new UnitStartupCommand
            {
                UnitId = mech.Id,
                IsAutomaticRestart = false,
                IsRestartPossible = false,
                GameOriginId = Guid.Empty, // Will be set by the calling phase
                AvoidShutdownRoll = null
            };
        }

        // Roll 2D6 to restart
        var diceRoll = _diceRoller.Roll2D6();
        var diceResults = diceRoll.Select(d => d.Result).ToArray();
        var rollTotal = diceResults.Sum();

        var restartSuccessful = rollTotal >= avoidNumber;
        
        var avoidShutdownRollData = new AvoidShutdownRollData
        {
            HeatLevel = currentHeat,
            DiceResults = diceResults,
            AvoidNumber = avoidNumber,
            IsSuccessful = restartSuccessful
        };

        return new UnitStartupCommand
        {
            UnitId = mech.Id,
            IsAutomaticRestart = false,
            IsRestartPossible = true,
            GameOriginId = Guid.Empty, // Will be set by the calling phase
            AvoidShutdownRoll = avoidShutdownRollData
        };
    }

    /// <summary>
    /// Gets the ammo explosion avoid number for a given heat level
    /// </summary>
    /// <param name="heatLevel">The current heat level</param>
    /// <returns>The 2D6 target number needed to avoid ammo explosion</returns>
    public int GetAmmoExplosionAvoidNumber(int heatLevel)
    {
        return _rulesProvider.GetHeatAmmoExplosionAvoidNumber(heatLevel);
    }

    public AmmoExplosionCommand? CheckForHeatAmmoExplosion(Mech mech)
    {
        var currentHeat = mech.CurrentHeat;
        var avoidNumber = GetAmmoExplosionAvoidNumber(currentHeat);

        // No explosion check needed
        if (avoidNumber < DiceUtils.Guaranteed2D6Roll)
            return null;

        // Find explodable ammo components
        var explodableAmmo = GetExplodableAmmoComponents(mech);
        if (explodableAmmo.Count == 0)
            return null;

        // Roll 2D6 to avoid explosion
        var diceRoll = _diceRoller.Roll2D6();
        var diceResults = diceRoll.Select(d => d.Result).ToArray();
        var rollTotal = diceResults.Sum();

        var explosionOccurs = rollTotal < avoidNumber;

        var avoidExplosionRollData = new AvoidAmmoExplosionRollData
        {
            HeatLevel = currentHeat,
            DiceResults = diceResults,
            AvoidNumber = avoidNumber,
            IsSuccessful = !explosionOccurs
        };

        List<LocationCriticalHitsData>? explosionCriticalHits = null;

        if (explosionOccurs)
        {
            // Select the most destructive ammo component to explode
            var selectedAmmo = SelectMostDestructiveAmmoComponent(explodableAmmo);
            explosionCriticalHits = ProcessAmmoExplosion(mech, selectedAmmo);
        }

        return new AmmoExplosionCommand
        {
            UnitId = mech.Id,
            AvoidExplosionRoll = avoidExplosionRollData,
            ExplosionCriticalHits = explosionCriticalHits,
            GameOriginId = Guid.Empty
        };
    }

    private List<Component> GetExplodableAmmoComponents(Mech mech)
    {
        return mech.Parts
            .SelectMany(part => part.Components)
            .Where(component => component is { CanExplode: true, HasExploded: false })
            .ToList();
    }

    private Component SelectMostDestructiveAmmoComponent(List<Component> explodableAmmo)
    {
        // Find the maximum explosion damage
        var maxDamage = explodableAmmo.Max(ammo => ammo.GetExplosionDamage());

        // Get all ammo components with the maximum damage
        var mostDestructiveAmmo = explodableAmmo
            .Where(ammo => ammo.GetExplosionDamage() == maxDamage)
            .ToList();

        // If there's only one, return it
        if (mostDestructiveAmmo.Count == 1)
            return mostDestructiveAmmo[0];

        // If there are multiple with the same damage, randomly select one
        var randomIndex = _diceRoller.RollD6().Result - 1; // Convert to 0-based
        return mostDestructiveAmmo[randomIndex % mostDestructiveAmmo.Count];
    }

    private List<LocationCriticalHitsData> ProcessAmmoExplosion(Mech mech, Component ammoComponent)
    {
        var location = ammoComponent.GetLocation();
        if (!location.HasValue) return [];

        var explosionDamage = ammoComponent.GetExplosionDamage();

        // Mark the component as exploded
        ammoComponent.Hit();

        // Use existing critical hits calculator to process the explosion
        return _criticalHitsCalculator.CalculateCriticalHits(mech, location.Value, explosionDamage);
    }
}
