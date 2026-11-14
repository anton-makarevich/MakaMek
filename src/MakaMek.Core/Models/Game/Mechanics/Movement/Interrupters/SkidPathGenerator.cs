using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

/// <summary>
/// Builds the path a skidding 'Mech travels after failing its skid PSR. Extracted from
/// <see cref="SkidInterruptHandler"/> so the handler only decides whether a skid happens and
/// what to do about it, while the map walking lives here.
/// </summary>
internal sealed class SkidPathGenerator
{
    /// <summary>
    /// Walks the map in the skid direction until the skid distance is spent, the map ends, the
    /// skid reaches water, or a cliff is found.
    /// </summary>
    public SkidPathResult Generate(
        ServerGame game,
        HexPositionData startPosition,
        int maxDistance,
        IUnit unit)
    {
        var skidPathSegments = new List<PathSegment>();
        var currentCoords = new HexCoordinates(startPosition.Coordinates);
        var skidFacing = (HexDirection)startPosition.Facing;
        var currentHex = game.BattleMap!.GetHex(currentCoords)!;
        var currentSurface = (HexSurface)startPosition.Surface;
        var remainingSkidDistance = maxDistance;

        while (remainingSkidDistance > 0)
        {
            var nextCoords = currentCoords.GetNeighbour(skidFacing);
            var nextHex = game.BattleMap?.GetHex(nextCoords);
            if (nextHex == null)
                break;

            var nextSurface = currentSurface switch
            {
                HexSurface.Bridge when nextHex.GetBridgeHeight() != null => HexSurface.Bridge,
                _ => HexSurface.Ground
            };

            if (nextSurface == HexSurface.Ground && nextHex.GetWaterDepth() >= 1)
            {
                skidPathSegments.Add(new PathSegment(
                    new HexPosition(currentCoords, skidFacing, currentSurface),
                    new HexPosition(nextCoords, skidFacing, nextSurface), [])
                {
                    Events = [new SegmentEvent(SegmentEventType.Skid)]
                });
                break;
            }

            var elevationChange = nextHex.GetElevationChange(currentHex, currentSurface, nextSurface);
            if (elevationChange < -unit.MaxLevelChangeForward)
            {
                var fromPos = new HexPosition(currentCoords, skidFacing, currentSurface);
                var toPos = new HexPosition(nextCoords, skidFacing, nextSurface);
                var skidSegment = new PathSegment(fromPos, toPos, [])
                {
                    Events = [new SegmentEvent(SegmentEventType.Skid)]
                };
                skidPathSegments.Add(skidSegment);

                return new SkidPathResult(
                    skidPathSegments,
                    true,
                    Math.Abs(elevationChange));
            }

            var movementCost = nextHex.GetEnterMovementCost(currentHex, currentSurface, nextSurface);
            var fromPos2 = new HexPosition(currentCoords, skidFacing, currentSurface);
            var toPos2 = new HexPosition(nextCoords, skidFacing, nextSurface);
            var normalSegment = new PathSegment(fromPos2, toPos2, [])
            {
                Events = [new SegmentEvent(SegmentEventType.Skid)]
            };
            skidPathSegments.Add(normalSegment);

            remainingSkidDistance -= movementCost.Sum(c => c.Value);
            currentCoords = nextCoords;
            currentHex = nextHex;
            currentSurface = nextSurface;
        }

        return new SkidPathResult(skidPathSegments, false, 0);
    }
}
