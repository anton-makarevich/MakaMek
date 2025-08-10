using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Calculator for heat effects including shutdown and restart mechanics
/// </summary>
public class HeatEffectsCalculator : IHeatEffectsCalculator
{
    private readonly IRulesProvider _rulesProvider;
    private readonly IDiceRoller _diceRoller;

    public HeatEffectsCalculator(IRulesProvider rulesProvider, IDiceRoller diceRoller)
    {
        _rulesProvider = rulesProvider;
        _diceRoller = diceRoller;
    }

    public UnitShutdownCommand? CheckForHeatShutdown(Mech mech, int currentTurn)
    {
        // Don't check if already shutdown
        if (mech.IsShutdown)
            return null;

        var currentHeat = mech.CurrentHeat;
        var shutdownThresholds = _rulesProvider.GetHeatShutdownThresholds();

        // Find the closest shutdown threshold that is less than or equal to current heat
        var applicableThreshold = shutdownThresholds
            .Where(threshold => currentHeat >= threshold)
            .DefaultIfEmpty(0)
            .Max();
        
        if (applicableThreshold == 0)
            return null;
        
        // Check for automatic shutdown at 30+ heat
        if (currentHeat >= 30)
        {
            var automaticShutdownData = new ShutdownData
            {
                Reason = ShutdownReason.Heat,
                Turn = currentTurn
            };

            var autoShutdownData = new AvoidShutdownRollData
            {
                HeatLevel = currentHeat,
                DiceResults = [],
                AvoidNumber = 13, // Impossible to roll
                IsSuccessful = false
            };
            
            return new UnitShutdownCommand
            {
                UnitId = mech.Id,
                ShutdownData = automaticShutdownData,
                AvoidShutdownRoll = autoShutdownData,
                IsAutomaticShutdown = true,
                GameOriginId = Guid.Empty // Will be set by the calling phase
            };
        }

        // Get the avoid number for the applicable threshold
        var avoidNumber = _rulesProvider.GetHeatShutdownAvoidNumber(applicableThreshold);
        if (!avoidNumber.HasValue)
            return null;

        // Check if pilot is conscious (unconscious pilots automatically fail)
        var isConsciousPilot = mech.Pilot?.IsConscious == true;
        
        bool shutdownOccurs;
        int[] diceResults = [];

        if (isConsciousPilot)
        {
            // Roll 2D6 to avoid shutdown
            var diceRoll = _diceRoller.Roll2D6();
            diceResults = diceRoll.Select(d => d.Result).ToArray();
            var rollTotal = diceResults.Sum();
            
            shutdownOccurs = rollTotal < avoidNumber.Value;
        }
        else
        {
            // Unconscious pilots automatically fail
            shutdownOccurs = true;
        }

        var shutdownData = new ShutdownData
        {
            Reason = ShutdownReason.Heat,
            Turn = currentTurn
        };

        var avoidShutdownRollData = new AvoidShutdownRollData
        {
            HeatLevel = applicableThreshold,
            DiceResults = diceResults,
            AvoidNumber = avoidNumber.Value,
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

        // Check for automatic restart due to low heat
        if (ShouldAutoRestart(mech))
        {
            var autoAvoidData = new AvoidShutdownRollData
            {
                HeatLevel = mech.CurrentHeat,
                DiceResults = [],
                AvoidNumber = 0,
                IsSuccessful = true
            };
            return new UnitStartupCommand
            {
                UnitId = mech.Id,
                IsAutomaticRestart = true,
                GameOriginId = Guid.Empty, // Will be set by the calling phase
                AvoidShutdownRoll = autoAvoidData
            };
        }

        // Only heat shutdowns require restart rolls
        if (shutdownData.Reason != ShutdownReason.Heat)
            return null;

        var currentHeat = mech.CurrentHeat;
        var avoidNumber = _rulesProvider.GetHeatShutdownAvoidNumber(currentHeat);

        // If no avoid number, can't restart (shouldn't happen)
        if (!avoidNumber.HasValue)
            return null;

        // Check if pilot is conscious (unconscious pilots automatically fail)
        var isConsciousPilot = mech.Pilot?.IsConscious == true;

        if (!isConsciousPilot)
            return null;

        // Roll 2D6 to restart
        var diceRoll = _diceRoller.Roll2D6();
        var diceResults = diceRoll.Select(d => d.Result).ToArray();
        var rollTotal = diceResults.Sum();

        var restartSuccessful = rollTotal >= avoidNumber.Value;
        
        var avoidShutdownRollData = new AvoidShutdownRollData
        {
            HeatLevel = currentHeat,
            DiceResults = diceResults,
            AvoidNumber = avoidNumber.Value,
            IsSuccessful = restartSuccessful
        };

        return new UnitStartupCommand
        {
            UnitId = mech.Id,
            IsAutomaticRestart = false,
            GameOriginId = Guid.Empty, // Will be set by the calling phase
            AvoidShutdownRoll = avoidShutdownRollData
        };
    }

    public bool ShouldAutoRestart(Mech mech)
    {
        // Must be shutdown to restart
        if (!mech.IsShutdown)
            return false;

        // Auto-restart when heat drops below the lowest shutdown threshold (14)
        var shutdownThresholds = _rulesProvider.GetHeatShutdownThresholds();
        var lowestThreshold = shutdownThresholds.Min();
        return mech.CurrentHeat < lowestThreshold;
    }
}
