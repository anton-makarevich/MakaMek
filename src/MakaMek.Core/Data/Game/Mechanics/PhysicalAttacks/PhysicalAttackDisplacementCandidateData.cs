using Sanet.MakaMek.Map.Data;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

/// <summary>
/// One adjacent hex offered to the target's controller after a missed Death From Above.
/// </summary>
/// <param name="Coordinates">The destination coordinates.</param>
/// <param name="Surface">The resolved surface as an <see cref="Map.Models.HexSurface"/> value.</param>
public record PhysicalAttackDisplacementCandidateData(HexCoordinateData Coordinates, int Surface);
