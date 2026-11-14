using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Evaluation;

/// <summary>
/// Projects mutable unit state into the immutable snapshots the evaluation collaborators consume.
/// </summary>
public interface IUnitSnapshotFactory
{
    /// <summary>
    /// Projects a unit as a target or defender: its current position, movement path and available weapons.
    /// </summary>
    UnitSnapshot Create(IUnit unit);

    /// <summary>
    /// Projects a unit as an attacker at the destination of <paramref name="attackerPath"/>,
    /// including the weapon configurations available from that position.
    /// </summary>
    UnitSnapshot CreateForAttacker(IUnit unit, MovementPath attackerPath);
}
