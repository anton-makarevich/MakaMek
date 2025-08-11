using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Utils;
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
        
        // Get the avoid number based on current heat level
        var avoidNumber = _rulesProvider.GetHeatShutdownAvoidNumber(currentHeat);
        
        // Check for automatic shutdown (avoidNumber 13 means automatic shutdown)
        if (avoidNumber == DiceUtils.Impossible2D6Roll)
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
        
        // If avoidNumber is 0, no shutdown check is needed
        if (avoidNumber < DiceUtils.Guaranteed2D6Roll)
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
            
            shutdownOccurs = rollTotal < avoidNumber;
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
                var avoidNumber = _rulesProvider.GetHeatShutdownAvoidNumber(currentHeat);
        
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
}
