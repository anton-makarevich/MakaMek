using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Map;

public static class HexCoordinatesExtensions
{
    extension(HexCoordinates coordinates)
    {
        /// <summary>
        /// Determines if a target hex is within a weapon firing arc from this hex
        /// </summary>
        /// <param name="targetCoordinates">The coordinates of the target hex</param>
        /// <param name="weapon">The weapon to check</param>
        /// <param name="facing">The direction the unit is facing, defaults to weapon facing, override to evaluate a hypothetical position</param>
        public bool IsInWeaponFiringArc(HexCoordinates targetCoordinates, Weapon weapon, HexDirection? facing = null)
        {
            facing ??= weapon.FirstMountPart?.Facing;
            if (facing == null)
                return false;
            
            var arcs = weapon.GetFiringArcs();
            foreach (var arc in arcs)
            {
                if (coordinates.IsInFiringArc(targetCoordinates, facing.Value, arc))
                    return true;
            }
            
            return false;
        }

        public bool IsOccupied(IGame game)
        {
            return game.Players.Any(p => p.Units.Any(u => u.IsDeployed && u.Position!.Coordinates == coordinates));
        }
    }
}