using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Game;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

using Components.Internal.Actuators;

public class Leg : UnitPart
{
    public Leg(string name, PartLocation location, int maxArmor, int maxStructure) 
        : base(name, location, maxArmor, maxStructure, 6)
    {
        // Add default components
        TryAddComponent(new HipActuator(), HipActuator.DefaultMountSlots);
        TryAddComponent(new UpperLegActuator(), UpperLegActuator.DefaultMountSlots);
        TryAddComponent(new LowerLegActuator(), LowerLegActuator.DefaultMountSlots);
        TryAddComponent(new FootActuator(), FootActuator.DefaultMountSlots);
    }

    internal override bool CanBeBlownOff => true;

    // Legs always face the direction of movement
    public override HexDirection? Facing => Unit?.Position?.Facing;

    public override IReadOnlyList<WeaponConfigurationOptions> GetWeaponsConfigurationOptions()
    {
        return new WeaponConfigurationOptions[] { };
    }
}