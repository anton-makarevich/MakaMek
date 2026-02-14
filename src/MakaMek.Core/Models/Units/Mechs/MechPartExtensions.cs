using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

public static class MechPartExtensions
{
    extension(UnitPart part)
    {
        public IReadOnlyList<WeaponConfigurationOptions> GetAvailableTorsoRotationOptions(HexPosition? forwardPosition = null)
        {
            forwardPosition ??= part.Unit?.Position;
            if (part.Unit is not Mech mech || forwardPosition == null || !mech.CanRotateTorso) return [];
            
            var currentFacingInt = (int)forwardPosition.Facing;
            
            var availableDirections = new List<HexDirection>();
            
            var possibleTorsoRotation = mech.PossibleTorsoRotation;

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

            return 
            [
                new WeaponConfigurationOptions(WeaponConfigurationType.TorsoRotation, availableDirections)
            ];
        }
    }
}
