namespace Sanet.MakaMek.Core.Models.Units.Mechs;

public class SideTorso(PartLocation location, int maxArmor, int maxRearArmor, int maxStructure)
    : Torso($"{location} Torso", location, maxArmor, maxRearArmor, maxStructure);