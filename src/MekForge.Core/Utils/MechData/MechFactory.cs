using Sanet.MekForge.Core.Models.Units;
using Sanet.MekForge.Core.Models.Units.Components;
using Sanet.MekForge.Core.Models.Units.Components.Engines;
using Sanet.MekForge.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MekForge.Core.Models.Units.Components.Weapons;
using Sanet.MekForge.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MekForge.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MekForge.Core.Models.Units.Mechs;
using Sanet.MekForge.Core.Utils.TechRules;

namespace Sanet.MekForge.Core.Utils.MechData;

public class MechFactory
{
    private readonly IRulesProvider _rulesProvider;

    public MechFactory( IRulesProvider rulesProvider)
    {
        _rulesProvider = rulesProvider;
    }

    public Mech Create(MechData mechData)
    {
        
        // Create parts with appropriate armor and structure
        var parts = CreateParts(mechData.ArmorValues, _rulesProvider, mechData.Mass);
        
        // Create the mech
        var mech = new Mech(
            mechData.Chassis,
            mechData.Model,
            mechData.Mass,
            mechData.WalkMp,
            parts);

        // Add equipment to parts
        AddEquipmentToParts(mech, mechData.LocationEquipment);

        return mech;
    }

    private static List<UnitPart> CreateParts(Dictionary<PartLocation, ArmorLocation> armorValues, IRulesProvider rulesProvider, int tonnage)
    {
        var structureValues = rulesProvider.GetStructureValues(tonnage);
        var parts = new List<UnitPart>();
        foreach (var (location, armor) in armorValues)
        {
            UnitPart part = location switch
            {
                PartLocation.LeftArm or PartLocation.RightArm => new Arm(location, armor.FrontArmor, structureValues[location]),
                PartLocation.LeftTorso or PartLocation.RightTorso => new SideTorso(location, armor.FrontArmor, armor.RearArmor, structureValues[location]),
                PartLocation.CenterTorso => new CenterTorso(armor.FrontArmor, armor.RearArmor, structureValues[location]),
                PartLocation.Head => new Head(armor.FrontArmor, structureValues[location]),
                PartLocation.LeftLeg or PartLocation.RightLeg => new Leg(location, armor.FrontArmor, structureValues[location]),
                _ => throw new ArgumentException($"Unknown location: {location}")
            };
            parts.Add(part);
        }
        return parts;
    }

    private void AddEquipmentToParts(Mech mech, Dictionary<PartLocation, List<string>> locationEquipment)
    {
        foreach (var (location, equipment) in locationEquipment)
        {
            var part = mech.Parts.First(p => p.Location == location);
            var componentCounts = new Dictionary<string, int>(); // Track component counts

            foreach (var item in equipment)
            {
                componentCounts.TryAdd(item, 0);
                componentCounts[item]++;

                var component = CreateComponent(item);
                if (component == null || componentCounts[item] < component.Size) continue;
                part.TryAddComponent(component);
                componentCounts[item] = 0; // Reset count after adding
            }
        }
    }

    private Component? CreateComponent(string itemName)
    {
        return itemName switch
        {
            "IS Ammo AC/5" => new Ammo(AmmoType.AC5, _rulesProvider.GetAmmoRounds(AmmoType.AC5)),
            "IS Ammo SRM-2" => new Ammo(AmmoType.SRM2, _rulesProvider.GetAmmoRounds(AmmoType.SRM2)),
            "IS Ammo MG - Full" => new Ammo(AmmoType.MachineGun, _rulesProvider.GetAmmoRounds(AmmoType.MachineGun)),
            "IS Ammo LRM-5" => new Ammo(AmmoType.LRM5, _rulesProvider.GetAmmoRounds(AmmoType.LRM5)), 
            "Machine Gun" => new MachineGun(),
            "Medium Laser" => new MediumLaser(),
            "Autocannon/5" => new AC5(),
            "Heat Sink" => new HeatSink(),
            "Shoulder" => new Shoulder(),
            "Upper Arm Actuator" => new UpperArmActuator(),
            "Fusion Engine" => new Engine("Fusion Engine", 160),
            _ => null
        };
    }
}
