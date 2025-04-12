using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Utils;

public class MechFactory
{
    private readonly IRulesProvider _rulesProvider;

    public MechFactory(IRulesProvider rulesProvider)
    {
        _rulesProvider = rulesProvider;
    }

    public Mech Create(UnitData unitData)
    {
        
        // Create parts with appropriate armor and structure
        var parts = CreateParts(unitData.ArmorValues, _rulesProvider, unitData.Mass);
        
        // Create the mech
        var mech = new Mech(
            unitData.Chassis,
            unitData.Model,
            unitData.Mass,
            unitData.WalkMp,
            parts,
            1,
            unitData.Id);
        
        // Add equipment to parts
        AddEquipmentToParts(mech, unitData);

        return mech;
    }

    private List<UnitPart> CreateParts(Dictionary<PartLocation, ArmorLocation> armorValues, IRulesProvider rulesProvider, int tonnage)
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

    private void AddEquipmentToParts(Mech mech, UnitData unitData)
    {
        foreach (var (location, equipment) in unitData.LocationEquipment)
        {
            var part = mech.Parts.First(p => p.Location == location);
            var componentCounts = new Dictionary<MakaMekComponent, int>(); // Track component counts

            foreach (var item in equipment)
            {
                componentCounts.TryAdd(item, 0);
                componentCounts[item]++;

                var component = CreateComponent(item, unitData);
                if (component == null || (componentCounts[item] < component.Size && component is not Engine)) continue;
                part.TryAddComponent(component);
                componentCounts[item] = 0; // Reset count after adding
            }
        }
    }

    private Component? CreateComponent(MakaMekComponent itemName, UnitData unitData)
    {
        return itemName switch
        {
            MakaMekComponent.Engine => new Engine(unitData.EngineRating, MapEngineType(unitData.EngineType)),
            MakaMekComponent.ISAmmoAC5 => new Ammo(AmmoType.AC5, _rulesProvider.GetAmmoRounds(AmmoType.AC5)),
            MakaMekComponent.ISAmmoSRM2 => new Ammo(AmmoType.SRM2, _rulesProvider.GetAmmoRounds(AmmoType.SRM2)),
            MakaMekComponent.ISAmmoMG => new Ammo(AmmoType.MachineGun, _rulesProvider.GetAmmoRounds(AmmoType.MachineGun)),
            MakaMekComponent.ISAmmoLRM5 => new Ammo(AmmoType.LRM5, _rulesProvider.GetAmmoRounds(AmmoType.LRM5)),
            MakaMekComponent.MediumLaser => new MediumLaser(),
            MakaMekComponent.LRM5 => new Lrm5(),
            MakaMekComponent.SRM2 => new Srm2(),
            MakaMekComponent.MachineGun => new MachineGun(),
            MakaMekComponent.AC5 => new Ac5(),
            MakaMekComponent.HeatSink => new HeatSink(),
            MakaMekComponent.Shoulder => new Shoulder(),
            MakaMekComponent.UpperArmActuator => new UpperArmActuator(),
            MakaMekComponent.LowerArmActuator => new LowerArmActuator(),
            MakaMekComponent.HandActuator => new HandActuator(),
            MakaMekComponent.JumpJet => new JumpJets(),
            MakaMekComponent.Gyro => null,
            MakaMekComponent.LifeSupport => null,
            MakaMekComponent.Sensors => null,
            MakaMekComponent.Cockpit => null,
            MakaMekComponent.Hip => null,
            MakaMekComponent.UpperLegActuator => null,
            MakaMekComponent.LowerLegActuator => null,
            MakaMekComponent.FootActuator => null,
            _ => throw new NotImplementedException($"{itemName} is not implemented")
        };
    }

    private EngineType MapEngineType(string engineType)
    {
        return engineType.ToLower() switch
        {
            "fusion" => EngineType.Fusion,
            "xlfusion" => EngineType.XLFusion,
            "ice" => EngineType.ICE,
            "light" => EngineType.Light,
            "compact" => EngineType.Compact,
            _ => throw new NotImplementedException($"Unknown engine type: {engineType}")
        };
    }
}
