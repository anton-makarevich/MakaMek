using Sanet.MakaMek.Map.Data;

namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Represents information about a covering hex that absorbed damage due to partial cover.
/// </summary>
/// <param name="CoveringHex">The coordinates of the hex that absorbed the damage</param>
/// <param name="AbsorbedDamage">The amount of damage absorbed by the covering hex</param>
public record CoveringHexData(
    HexCoordinateData CoveringHex,
    int AbsorbedDamage);
