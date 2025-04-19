namespace Sanet.MakaMek.Core.Models.Units.Mechs;

public class SideTorso : Torso
{
    public SideTorso(string name, PartLocation location, int maxArmor, int maxRearArmor, int maxStructure)
        : base(name, location, maxArmor, maxRearArmor, maxStructure)
    {
    }
}