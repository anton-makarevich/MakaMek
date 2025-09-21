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
}