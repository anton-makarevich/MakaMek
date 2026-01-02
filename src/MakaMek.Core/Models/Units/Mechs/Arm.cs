using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

public class Arm : UnitPart
{
    public Arm(string name, PartLocation location, int maxArmor, int maxStructure) 
        : base(name, location, maxArmor, maxStructure, 12)
    {
        // Add default components
        TryAddComponent(new ShoulderActuator(), ShoulderActuator.DefaultMountSlots);
    }

    internal override bool CanBeBlownOff => true;
    public override IReadOnlyList<FiringArc> GetFiringArcs(MountingOptions mountingOptions)
    {
        return
        [
            FiringArc.Front, //TODO: once arm flip is implemented it should be considered
            Location == PartLocation.LeftArm ? FiringArc.Left : FiringArc.Right
        ];
    }

    public override IReadOnlyList<WeaponConfigurationOptions> GetWeaponsConfigurationOptions(HexPosition? forwardPosition = null)
    {
        forwardPosition ??= Unit?.Position;
        if (Unit is not Mech mech || forwardPosition == null)
        {
            return [];
        }

        var result =  this.GetAvailableTorsoRotationOptions(forwardPosition)
            .ToList();

        if (mech.CanFlipArms)
        {
            var oppositeFacing = forwardPosition.Facing.GetOppositeDirection();
            result.Add(new WeaponConfigurationOptions(
                WeaponConfigurationType.ArmsFlip,
                [oppositeFacing]));
        }

        return result;
    }
}