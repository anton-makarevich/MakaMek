using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Evaluation;

/// <summary>
/// Evaluation helpers that operate on a projected weapon snapshot rather than the domain weapon.
/// </summary>
public static class WeaponSnapshotExtensions
{
    extension(WeaponSnapshot weapon)
    {
        /// <summary>
        /// Checks whether a target hex is inside one of the weapon's firing arcs.
        /// Mirrors <c>HexCoordinates.IsInWeaponFiringArc</c> without reading domain state.
        /// </summary>
        /// <param name="origin">Coordinates the weapon fires from.</param>
        /// <param name="target">Coordinates being targeted.</param>
        /// <param name="facing">Facing to evaluate; defaults to the mounting part's facing.</param>
        public bool IsInFiringArc(HexCoordinates origin, HexCoordinates target, HexDirection? facing = null)
        {
            var resolvedFacing = facing ?? weapon.MountFacing;
            if (resolvedFacing == null)
                return false;

            foreach (var arc in weapon.FiringArcs)
            {
                if (origin.IsInFiringArc(target, resolvedFacing.Value, arc))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether the weapon can fire at the given range.
        /// </summary>
        /// <param name="range">Distance to the target in hexes.</param>
        public bool CanReach(int range)
            => weapon.LongRange != null && range <= weapon.LongRange.Value;
    }
}
