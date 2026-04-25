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

    /// <summary>
    /// Processes a water entry PSR for a mech entering a water hex.
    /// The water depth is used to determine the difficulty modifier for the roll.
    /// </summary>
    /// <param name="mech">The mech entering the water hex</param>
    /// <param name="waterDepth">The depth of the water hex (1, 2, or 3+)</param>
    /// <param name="game">The game instance</param>
    /// <returns>FallContextData containing the result of the water entry PSR</returns>
    FallContextData ProcessWaterEntry(Mech mech, int waterDepth, IGame game);
}