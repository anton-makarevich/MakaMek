using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Map;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

public static class MechPartExtensions
{
    extension(UnitPart part)
    {
        public IReadOnlyList<WeaponConfigurationOptions> GetAvailableTorsoRotationOptions()
        {
            if (part.Unit is not Mech mech || mech.Position == null || !mech.CanRotateTorso) return [];
            
            var currentFacingInt = (int)mech.Position.Facing;
            
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
