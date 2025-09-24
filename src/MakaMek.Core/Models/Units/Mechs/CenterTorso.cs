using Sanet.MakaMek.Core.Models.Units.Components.Internal;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

public class CenterTorso : Torso
{
    public CenterTorso(string name, int maxArmor, int maxRearArmor, int maxStructure) 
        : base(name, PartLocation.CenterTorso, maxArmor, maxRearArmor, maxStructure)
    {
        // Add default components
        TryAddComponent(new Gyro(), Gyro.DefaultMountSlots);
    }
}