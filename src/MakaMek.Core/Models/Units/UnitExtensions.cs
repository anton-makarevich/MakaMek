using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Models;


namespace Sanet.MakaMek.Core.Models.Units;

public static class UnitExtensions
{
    /// <param name="unit">The unit to clone</param>
    extension(IUnit unit)
    {
        /// <summary>
        /// Creates a deep copy of the unit by converting it to data and recreating it
        /// </summary>
        /// <param name="mechFactory">The factory to use for creating the cloned unit</param>
        /// <returns>A new unit instance with the same state as the original</returns>
        public Unit CloneUnit(IMechFactory mechFactory)
        {
            var data = unit.ToData();
            return mechFactory.Create(data);
        }

        /// <summary>
        /// Whether the unit is a Death From Above attacker still waiting in its approach hex. Such a
        /// unit is airborne for line-of-sight purposes during the Weapon Attack Phase; the airborne
        /// base level is stored on <see cref="IUnit.Position"/> by the shared move handler.
        /// </summary>
        public bool IsAirborneDfaAttacker =>
            unit.HasPendingMovementAttack
            && unit.DeclaredPhysicalAttack.IsDfa()
            && unit.Position?.AirborneLevel is not null;

        /// <summary>
        /// Determines the direction an attack enters this unit's hex, taken from this unit's own
        /// facing. A missing position on either unit falls back to the front.
        /// </summary>
        /// <param name="attacker">The attacking unit.</param>
        /// <returns>The direction of the attack relative to this unit.</returns>
        public HitDirection GetAttackDirection(IUnit? attacker)
        {
            if (attacker?.Position == null || unit.Position == null)
                return HitDirection.Front;

            return unit.GetAttackDirectionFrom(attacker.Position.Coordinates);
        }

        /// <summary>
        /// Determines the direction an attack enters this unit's hex from an explicit origin
        /// coordinate, measured against this unit's own facing. A Death From Above uses the hex
        /// where the attacker started its jump instead of its airborne position.
        /// </summary>
        /// <param name="fromCoordinates">The hex the attack is measured from.</param>
        /// <returns>The direction of the attack relative to this unit.</returns>
        public HitDirection GetAttackDirectionFrom(HexCoordinates fromCoordinates)
        {
            if (unit.Position == null || unit.Position.Coordinates == fromCoordinates)
                return HitDirection.Front;

            var arc = unit.Position.Coordinates.GetFiringArc(
                fromCoordinates,
                unit.Position.Facing);

            return arc switch
            {
                FiringArc.Left => HitDirection.Left,
                FiringArc.Right => HitDirection.Right,
                FiringArc.Rear => HitDirection.Rear,
                _ => HitDirection.Front
            };
        }
    }
}
