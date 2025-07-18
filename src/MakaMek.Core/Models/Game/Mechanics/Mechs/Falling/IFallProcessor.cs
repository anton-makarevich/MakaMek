using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

public interface IFallProcessor
{
    /// <summary>
    /// Checks for conditions that might cause a unit to fall and prepares a MechFallingCommand if a fall occurs.
    /// </summary>
    /// <param name="mech">The unit to check, must be a mech.</param>
    /// <param name="game">Current game.</param>
    /// <param name="componentHits">A list of component hits sustained by the unit in the current context.</param>
    /// <param name="destroyedPartLocations">The locations of parts that were destroyed.</param>
    /// <returns>A collection of MechFallCommands if the unit falls, otherwise null.</returns>
    IEnumerable<MechFallCommand> ProcessPotentialFall(
        Mech mech, 
        IGame game,
        List<ComponentHitData> componentHits,
        List<PartLocation>? destroyedPartLocations = null);

    /// <summary>
    /// Processes a movement attempt that may result in a fall for a unit
    /// </summary>
    /// <param name="mech">The mech performing the movement attempt</param>
    /// <param name="possibleFallReason">FallReason/PSR type during movement phase</param>
    /// <param name="game">The game instance</param>
    /// <returns>FallContextData containing the result of the movement attempt</returns>
    FallContextData ProcessMovementAttempt(Mech mech, FallReasonType possibleFallReason, IGame game);
}