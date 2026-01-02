using Sanet.MakaMek.Core.Models.Map;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

public static class MechPartsExtensions
{
    public static IReadOnlyList<HexDirection> GetAvailableTorsoRotationDirections(
        HexDirection currentFacing,
        int possibleTorsoRotation)
    {
        if (possibleTorsoRotation <= 0)
        {
            return [];
        }

        var currentFacingInt = (int)currentFacing;
        var availableDirections = new List<HexDirection>();

        for (var i = 0; i < 6; i++)
        {
            var clockwiseSteps = (i - currentFacingInt + 6) % 6;
            var counterClockwiseSteps = (currentFacingInt - i + 6) % 6;
            var steps = Math.Min(clockwiseSteps, counterClockwiseSteps);

            if (steps <= possibleTorsoRotation && steps > 0)
            {
                availableDirections.Add((HexDirection)i);
            }
        }

        return availableDirections;
    }
}
