namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Direction helpers that relate a hex to an arbitrary (not necessarily adjacent) origin. They
/// project the six neighbouring hex directions onto a cube-space vector so a caller such as a
/// Death From Above displacement can pick a deterministic direction instead of a random one.
/// </summary>
public static class HexCoordinatesExtensions
{
    /// <param name="coordinates">The hex the direction is evaluated at.</param>
    extension(HexCoordinates coordinates)
    {
        /// <summary>
        /// Gets the neighbouring direction that points away from <paramref name="origin"/>. Ties
        /// are broken by the stable <see cref="HexDirection"/> order.
        /// </summary>
        public HexDirection GetDirectionAwayFrom(HexCoordinates origin) =>
            coordinates.GetDirectionsByAlignmentAwayFrom(origin)[0];

        /// <summary>
        /// Gets all six neighbouring directions ordered by how closely they point away from
        /// <paramref name="origin"/>: the best alignment first, ties by direction index. A caller
        /// can walk the list to fall back from the ideal hex to the closest available one.
        /// </summary>
        public IReadOnlyList<HexDirection> GetDirectionsByAlignmentAwayFrom(HexCoordinates origin)
        {
            if (coordinates.Equals(origin))
                return HexDirectionExtensions.AllDirections;

            var dx = coordinates.X - origin.X;
            var dy = coordinates.Y - origin.Y;
            var dz = coordinates.Z - origin.Z;

            return HexDirectionExtensions.AllDirections
                .Select(direction =>
                {
                    var neighbour = coordinates.GetNeighbour(direction);
                    var dot = dx * (neighbour.X - coordinates.X)
                              + dy * (neighbour.Y - coordinates.Y)
                              + dz * (neighbour.Z - coordinates.Z);
                    return (Direction: direction, Alignment: dot);
                })
                .OrderByDescending(candidate => candidate.Alignment)
                .ThenBy(candidate => (int)candidate.Direction)
                .Select(candidate => candidate.Direction)
                .ToList();
        }
    }
}
