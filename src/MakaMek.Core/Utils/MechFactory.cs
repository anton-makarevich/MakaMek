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
    private readonly IComponentDefinitionRegistry _componentRegistry;

    public MechFactory(IRulesProvider rulesProvider, ILocalizationService localizationService, IComponentDefinitionRegistry componentRegistry)
    {
        _rulesProvider = rulesProvider;
        _localizationService = localizationService;
        _componentRegistry = componentRegistry;
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
        if (!unitData.Equipment.Any())
            return;

        AddEquipmentFromComponentData(mech, unitData);
    }

    private void AddEquipmentFromComponentData(Mech mech, UnitData unitData)
    {
        foreach (var componentData in unitData.Equipment)
        {
            var component = CreateComponent(componentData, unitData);
            if (component == null) continue;

            // Mount to additional locations (without adding to their component lists)
            foreach (var assignment in componentData.Assignments)
            {
                var part = mech.Parts[assignment.Location];
                var slots = assignment.Slots.ToArray();

                // Mount the component to this location using the Mount method directly
                component.Mount(slots, part);
            }
        }
    }

    private Component? CreateComponent(ComponentData componentData, UnitData unitData)
    {
        // Handle special cases that need additional data from UnitData
        if (componentData.Type == MakaMekComponent.Engine)
        {
            // Create engine with specific rating and type from UnitData
            var engineData = new ComponentData
            {
                Type = MakaMekComponent.Engine,
                Assignments = componentData.Assignments,
                Hits = componentData.Hits,
                IsActive = componentData.IsActive,
                HasExploded = componentData.HasExploded,
                SpecificData = new EngineStateData(unitData.EngineRating, MapEngineType(unitData.EngineType))
            };
            return _componentRegistry.CreateComponent(componentData.Type, engineData);
        }

        // Use registry for all other components
        return _componentRegistry.CreateComponent(componentData.Type, componentData);
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
