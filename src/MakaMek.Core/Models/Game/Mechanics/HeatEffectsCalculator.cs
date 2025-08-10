using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;
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

    public MechShutdownCommand? CheckForHeatShutdown(Mech mech, int previousHeat, int currentTurn)
    {
        // Don't check if already shutdown
        if (mech.IsShutdown)
            return null;

        var currentHeat = mech.CurrentHeat;
        var shutdownThresholds = _rulesProvider.GetHeatShutdownThresholds();

        // Find the highest threshold crossed this turn
        var highestThresholdCrossed = shutdownThresholds
            .Where(threshold => currentHeat >= threshold && previousHeat < threshold)
            .DefaultIfEmpty(0)
            .Max();

        if (highestThresholdCrossed == 0)
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
                AvoidNumber = 13 // Impossible to roll
            };
            
            return new MechShutdownCommand
            {
                UnitId = mech.Id,
                ShutdownData = automaticShutdownData,
                AvoidShutdownRoll = autoShutdownData,
                IsAutomaticShutdown = true,
                GameOriginId = Guid.Empty, // Will be set by the calling phase
                Timestamp = DateTime.UtcNow
            };
        }

        // Get the avoid number for the highest threshold crossed
        var avoidNumber = _rulesProvider.GetHeatShutdownAvoidNumber(highestThresholdCrossed);
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

        if (!shutdownOccurs)
            return null;

        var shutdownData = new ShutdownData
        {
            Reason = ShutdownReason.Heat,
            Turn = currentTurn
        };
        
        var avoidShutdownRollData = new AvoidShutdownRollData
        {
            HeatLevel = highestThresholdCrossed,
            DiceResults = diceResults,
            AvoidNumber = avoidNumber.Value
        };

        return new MechShutdownCommand
        {
            UnitId = mech.Id,
            ShutdownData = shutdownData,
            AvoidShutdownRoll = avoidShutdownRollData,
            IsAutomaticShutdown = false,
            GameOriginId = Guid.Empty, // Will be set by the calling phase
            Timestamp = DateTime.UtcNow
        };
    }

    public bool AttemptRestart(Mech mech, int currentTurn)
    {
        // Must be shutdown to restart
        if (!mech.IsShutdown || !mech.CurrentShutdownData.HasValue)
            return false;

        var shutdownData = mech.CurrentShutdownData.Value;

        // Can't restart in the same turn as shutdown
        if (shutdownData.Turn >= currentTurn)
            return false;

        // Check for automatic restart due to low heat
        if (ShouldAutoRestart(mech))
        {
            return true;
        }

        // Only heat shutdowns require restart rolls
        if (shutdownData.Reason != ShutdownReason.Heat)
            return false;

        var currentHeat = mech.CurrentHeat;
        var avoidNumber = _rulesProvider.GetHeatShutdownAvoidNumber(currentHeat);

        // If no avoid number, can't restart (shouldn't happen)
        if (!avoidNumber.HasValue)
            return false;

        // Check if pilot is conscious (unconscious pilots automatically fail)
        var isConsciousPilot = mech.Pilot?.IsConscious == true;
        
        if (!isConsciousPilot)
            return false;

        // Roll 2D6 to restart
        var diceRoll = _diceRoller.Roll2D6();
        var rollTotal = diceRoll.Sum(d => d.Result);
        
        var restartSuccessful = rollTotal >= avoidNumber.Value;
        
        return restartSuccessful;
    }

    public bool ShouldAutoRestart(Unit unit)
    {
        // Must be shutdown to restart
        if (!unit.IsShutdown)
            return false;

        // Auto-restart when heat drops below threshold
        var autoRestartThreshold = _rulesProvider.GetAutoRestartHeatThreshold();
        return unit.CurrentHeat < autoRestartThreshold;
    }
}
