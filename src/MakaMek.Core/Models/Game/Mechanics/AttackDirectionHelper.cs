using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// The single definition of the direction an attack enters the target's hex, taken from the
/// target's own facing. The weapon resolver and the physical attack resolver share it, so a
/// punch and a weapon hit always agree on the column of the hit location table.
/// </summary>
internal static class AttackDirectionHelper
{
    /// <summary>
    /// Determines the direction the attack enters the target's hex relative to the target's
    /// facing. A missing position on either unit falls back to the front.
    /// </summary>
    /// <param name="attacker">The attacking unit.</param>
    /// <param name="target">The attacked unit.</param>
    /// <returns>The direction of the attack relative to the target.</returns>
    public static HitDirection DetermineAttackDirection(IUnit? attacker, IUnit target)
    {
        if (attacker?.Position == null || target.Position == null)
            return HitDirection.Front;

        var arc = target.Position.Coordinates.GetFiringArc(
            attacker.Position.Coordinates, target.Position.Facing);

        return arc switch
        {
            FiringArc.Left => HitDirection.Left,
            FiringArc.Right => HitDirection.Right,
            FiringArc.Rear => HitDirection.Rear,
            _ => HitDirection.Front
        };
    }
}
