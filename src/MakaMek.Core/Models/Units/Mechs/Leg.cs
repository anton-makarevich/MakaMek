namespace Sanet.MakaMek.Core.Models.Units.Mechs;

using Components.Internal.Actuators;

public class Leg : UnitPart
{
    public Leg(string name, PartLocation location, int maxArmor, int maxStructure) 
        : base(name, location, maxArmor, maxStructure, 6)
    {
        // Add default components
        TryAddComponent(new Hip());
        TryAddComponent(new UpperLegActuator());
        TryAddComponent(new LowerLegActuator());
        TryAddComponent(new FootActuator());
    }
}