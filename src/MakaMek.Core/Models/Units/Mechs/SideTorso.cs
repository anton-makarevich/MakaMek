namespace Sanet.MakaMek.Core.Models.Units.Mechs;

public class SideTorso : Torso
{
    public SideTorso(string name, PartLocation location, int maxArmor, int maxRearArmor, int maxStructure)
        : base(name, location, maxArmor, maxRearArmor, maxStructure)
    {
    }

    public override void ApplyBreach()
    {
        base.ApplyBreach();

        if (Location is not (PartLocation.LeftTorso or PartLocation.RightTorso)) return;
        var armLocation = Location == PartLocation.LeftTorso ? PartLocation.LeftArm : PartLocation.RightArm;
        if (Unit?.Parts.TryGetValue(armLocation, out var armPart) != true) return;
        foreach (var component in armPart?.Components??[])
        {
            component.Flood();
        }
    }
}