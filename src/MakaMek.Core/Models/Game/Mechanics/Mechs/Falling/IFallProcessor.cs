using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

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
    /// Processes a movement attempt that may result in a fall for a unit, including entering a water hex.
    /// Pass an <see cref="Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts.EnteringDeepWaterRollContext"/>
    /// to handle water-entry PSRs without a separate method.
    /// </summary>
    /// <param name="mech">The mech performing the movement attempt</param>
    /// <param name="rollContext">The PSR context for this movement attempt</param>
    /// <param name="game">The game instance</param>
    /// <param name="movementType">The type of movement being attempted</param>
    /// <returns>FallContextData containing the result of the movement attempt</returns>
    FallContextData ProcessMovementAttempt(Mech mech, PilotingSkillRollContext rollContext, IGame game, MovementType movementType);

    /// <summary>
    /// Creates a cliff fall context for a fall that occurs when a mech skids off a cliff edge.
    /// The fall is automatic (no PSR required) with the specified levels fallen and shares the
    /// facing direction from the initial slkdfall.
    /// </summary>
    /// <param name="mech">The mech falling off the cliff</param>
    /// <param name="levelsFallen">Number of levels the mech falls</param>
    /// <param name="game">The game instance</param>
    /// <param name="facingDiceRoll">The dice result that determines facing direction</param>
    /// <param name="facingAfterFall">The mech's facing after the fall</param>
    /// <returns>FallContextData with IsFalling = true</returns>
    FallContextData CreateCliffFallContext(Mech mech, int levelsFallen, IGame game, DiceResult facingDiceRoll, HexDirection facingAfterFall);
}