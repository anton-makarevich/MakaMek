using Sanet.MakaMek.Map.Data;

namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Represents a position on the hex map, combining coordinates and facing direction
/// </summary>
/// <param name="AirborneLevel">
/// The absolute base/elevation level of the unit at this position while it is airborne, or
/// <c>null</c> when the position is grounded. It is the unit's base level, not its LOS top:
/// consumers add the unit or mount height. Jump pathfinding writes it on the jump-profile positions.
/// After a completed move the only stored non-null case is a pending Death From Above attacker's
/// approach position; the server re-derives that value from the map when it applies the move.
/// </param>
public record HexPosition(
    HexCoordinates Coordinates,
    HexDirection Facing,
    HexSurface Surface = HexSurface.Ground,
    int? AirborneLevel = null)
{
    public HexPosition(int q, int r, HexDirection facing, HexSurface surface = HexSurface.Ground)
        : this(new HexCoordinates(q, r), facing, surface)
    {
    }

    public HexPosition(HexPositionData data)
        : this(new HexCoordinates(data.Coordinates), (HexDirection)data.Facing, (HexSurface)data.Surface,
            data.AirborneLevel)
    {
    }

    /// <summary>
    /// Calculates the cost in movement points to turn from current facing to target facing
    /// </summary>
    public int GetTurningCost(HexDirection targetFacing)
    {
        var diff = Math.Abs((int)targetFacing - (int)Facing);
        return Math.Min(diff, 6 - diff); // Consider both clockwise and counterclockwise turns
    }

    /// <summary>
    /// Gets a sequence of positions representing the turning steps from current facing to target facing
    /// </summary>
    public IEnumerable<HexPosition> GetTurningSteps(HexDirection targetFacing)
    {
        if (Facing == targetFacing)
            yield break;

        var currentFacingInt = (int)Facing;
        var targetFacingInt = (int)targetFacing;
        
        var clockwiseSteps = (targetFacingInt - currentFacingInt + 6) % 6;
        var counterClockwiseSteps = (currentFacingInt - targetFacingInt + 6) % 6;

        // Choose the shorter turning direction
        if (clockwiseSteps <= counterClockwiseSteps)
        {
            // Turn clockwise
            for (var step = 1; step <= clockwiseSteps; step++)
            {
                var intermediateFacing = (HexDirection)((currentFacingInt + step) % 6);
                yield return this with { Facing = intermediateFacing };
            }
        }
        else
        {
            // Turn counterclockwise
            for (var step = 1; step <= counterClockwiseSteps; step++)
            {
                var intermediateFacing = (HexDirection)((currentFacingInt - step + 6) % 6);
                yield return this with { Facing = intermediateFacing };
            }
        }
    }

    public HexPositionData ToData() => new()
    {
        Coordinates = Coordinates.ToData(),
        Facing = (int)Facing,
        Surface = (int)Surface,
        AirborneLevel = AirborneLevel
    };

    /// <summary>
    /// Returns a new position with the same coordinates but facing the opposite direction
    /// </summary>
    public HexPosition GetOppositeDirectionPosition() =>
        this with { Facing = (HexDirection)((int)(Facing + 3) % 6) };
}
