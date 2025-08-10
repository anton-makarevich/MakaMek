using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Interface for calculating heat effects including shutdown and restart mechanics
/// </summary>
public interface IHeatEffectsCalculator
{
    /// <summary>
    /// Checks if a mech should attempt a shutdown roll based on heat thresholds
    /// and performs the roll if necessary
    /// </summary>
    /// <param name="mech">The mech to check for shutdown</param>
    /// <param name="previousHeat">The unit's heat level before this turn</param>
    /// <param name="currentTurn">The current game turn</param>
    /// <returns>Shutdown command if shutdown occurs, null otherwise</returns>
    MechShutdownCommand? CheckForHeatShutdown(Mech mech, int previousHeat, int currentTurn);
    
    /// <summary>
    /// Attempts to restart a shutdown mech
    /// </summary>
    /// <param name="mech">The shutdown mech to attempt restart</param>
    /// <param name="currentTurn">The current game turn</param>
    /// <returns>True if restart was successful, false otherwise</returns>
    bool AttemptRestart(Mech mech, int currentTurn);
    
    /// <summary>
    /// Checks if a unit should automatically restart due to low heat
    /// </summary>
    /// <param name="unit">The unit to check</param>
    /// <returns>True if the unit should automatically restart</returns>
    bool ShouldAutoRestart(Unit unit);
}
