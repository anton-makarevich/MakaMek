using Sanet.MakaMek.Core.Models.Units.Components.Internal;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

public class Head : UnitPart
{
    public Head(string name, int maxArmor, int maxStructure) 
        : base(name, PartLocation.Head, maxArmor, maxStructure, 12)
    {
        // Add default components
        TryAddComponent(new LifeSupport());
        TryAddComponent(new Sensors());
        TryAddComponent(new Cockpit());
    }
}