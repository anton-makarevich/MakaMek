using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Melee;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Utils;

public class MechFactory : IMechFactory
{
    private readonly IRulesProvider _rulesProvider;
    private readonly ILocalizationService _localizationService;

    public MechFactory(IRulesProvider rulesProvider, ILocalizationService localizationService)
    {
        _rulesProvider = rulesProvider;
        _localizationService = localizationService;
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
            var name = _localizationService.GetString($"MechPart_{location}");
            UnitPart part = location switch
            {
                PartLocation.LeftArm or PartLocation.RightArm => new Arm(name, location, armor.FrontArmor, structureValues[location]),
                PartLocation.LeftTorso or PartLocation.RightTorso => new SideTorso(name, location, armor.FrontArmor, armor.RearArmor, structureValues[location]),
                PartLocation.CenterTorso => new CenterTorso(name, armor.FrontArmor, armor.RearArmor, structureValues[location]),
                PartLocation.Head => new Head(name, armor.FrontArmor, structureValues[location]),
                PartLocation.LeftLeg or PartLocation.RightLeg => new Leg(name, location, armor.FrontArmor, structureValues[location]),
                _ => throw new ArgumentException($"Unknown location: {location}")
            };
            parts.Add(part);
        }
        return parts;
    }

    private void AddEquipmentToParts(Mech mech, UnitData unitData)
    {
        // Group components by type to handle multi-location components
        var componentsByType = new Dictionary<MakaMekComponent, List<(PartLocation location, int[] slots)>>();

        foreach (var (location, slotLayout) in unitData.LocationEquipment)
        {
            foreach (var componentAssignment in slotLayout.ComponentAssignments)
            {
                if (!componentsByType.ContainsKey(componentAssignment.Component))
                {
                    componentsByType[componentAssignment.Component] = [];
                }
                componentsByType[componentAssignment.Component].Add((location, componentAssignment.Slots));
            }
        }

        // Create and mount components
        foreach (var (componentType, locationSlots) in componentsByType)
        {
            var component = CreateComponent(componentType, unitData);
            if (component == null) continue;

            // Handle multi-location components
            foreach (var (location, slots) in locationSlots)
            {
                var part = mech.Parts[location];

                // Add component to unit part
                part.TryAddComponent(component, slots);
            }
        }
    }

    private Component? CreateComponent(MakaMekComponent itemName, UnitData unitData)
    {
        return itemName switch
        {
            MakaMekComponent.Engine => new Engine(unitData.EngineRating, MapEngineType(unitData.EngineType)),
            // Ammunition
            MakaMekComponent.ISAmmoAC2 => Ac2.CreateAmmo(),
            MakaMekComponent.ISAmmoAC5 =>Ac5.CreateAmmo(),
            MakaMekComponent.ISAmmoAC10 => Ac10.CreateAmmo(),
            MakaMekComponent.ISAmmoAC20 => Ac20.CreateAmmo(),
            MakaMekComponent.ISAmmoSRM2 => Srm2.CreateAmmo(),
            MakaMekComponent.ISAmmoSRM4 => Srm4.CreateAmmo(),
            MakaMekComponent.ISAmmoSRM6 => Srm6.CreateAmmo(),
            MakaMekComponent.ISAmmoLRM5 => Lrm5.CreateAmmo(),
            MakaMekComponent.ISAmmoLRM10 => Lrm10.CreateAmmo(),
            MakaMekComponent.ISAmmoLRM15 => Lrm15.CreateAmmo(),
            MakaMekComponent.ISAmmoLRM20 => Lrm20.CreateAmmo(),
            MakaMekComponent.ISAmmoMG => MachineGun.CreateAmmo(),
            // Energy Weapons
            MakaMekComponent.SmallLaser => new SmallLaser(),
            MakaMekComponent.MediumLaser => new MediumLaser(),
            MakaMekComponent.LargeLaser => new LargeLaser(),
            MakaMekComponent.PPC => new Ppc(),
            MakaMekComponent.Flamer => new Flamer(),
            // Ballistic Weapons
            MakaMekComponent.AC2 => new Ac2(),
            MakaMekComponent.AC5 => new Ac5(),
            MakaMekComponent.AC10 => new Ac10(),
            MakaMekComponent.AC20 => new Ac20(),
            MakaMekComponent.MachineGun => new MachineGun(),
            // Missile Weapons
            MakaMekComponent.LRM5 => new Lrm5(),
            MakaMekComponent.LRM10 => new Lrm10(),
            MakaMekComponent.LRM15 => new Lrm15(),
            MakaMekComponent.LRM20 => new Lrm20(),
            MakaMekComponent.SRM2 => new Srm2(),
            MakaMekComponent.SRM4 => new Srm4(),
            MakaMekComponent.SRM6 => new Srm6(),
            // Melee Weapons
            MakaMekComponent.Hatchet => new Hatchet(),
            MakaMekComponent.HeatSink => new HeatSink(),
            MakaMekComponent.Shoulder => new ShoulderActuator(),
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
