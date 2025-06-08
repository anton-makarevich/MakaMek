using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

public interface IFallProcessor
{
    /// <summary>
    /// Checks for conditions that might cause a unit to fall and prepares a MechFallingCommand if a fall occurs.
    /// </summary>
    /// <param name="unit">The unit to check.</param>
    /// <param name="battleMap">The battle map, used for some PSR calculations.</param>
    /// <param name="componentHits">A list of component hits sustained by the unit in the current context.</param>
    /// <param name="totalDamage">The total damage sustained by the unit in the current context.</param>
    /// <param name="gameId">The ID of the current game.</param>
    /// <param name="destroyedPartLocations">The locations of parts that were destroyed.</param>
    /// <returns>A collection of MechFallingCommands if the unit falls, otherwise null.</returns>
    IEnumerable<MechFallingCommand> ProcessPotentialFall(
        Unit unit,
        BattleMap? battleMap,
        List<ComponentHitData> componentHits,
        int totalDamage,
        Guid gameId,
        List<PartLocation>? destroyedPartLocations = null);
}