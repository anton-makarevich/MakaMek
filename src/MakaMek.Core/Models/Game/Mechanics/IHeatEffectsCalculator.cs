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
    /// <param name="currentTurn">The current game turn</param>
    /// <returns>Shutdown command if threshold is crossed, null otherwise</returns>
    UnitShutdownCommand? CheckForHeatShutdown(Mech mech, int currentTurn);

    /// <summary>
    /// Attempts to restart a shutdown mech
    /// </summary>
    /// <param name="mech">The shutdown mech to attempt restart</param>
    /// <param name="currentTurn">The current game turn</param>
    /// <returns>Restart command with success/failure information</returns>
    UnitStartupCommand? AttemptRestart(Mech mech, int currentTurn);
}
