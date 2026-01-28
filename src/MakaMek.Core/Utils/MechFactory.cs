using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Utils;

public class MechFactory : IMechFactory
{
    private readonly IRulesProvider _rulesProvider;
    private readonly ILocalizationService _localizationService;
    private readonly IComponentProvider _componentProvider;

    public MechFactory(IRulesProvider rulesProvider,  IComponentProvider componentProvider, ILocalizationService localizationService)
    {
        _rulesProvider = rulesProvider;
        _localizationService = localizationService;
        _componentProvider = componentProvider;
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
            parts,
            1,
            false,
            unitData.Id);

        // Add equipment to parts
        AddEquipmentToParts(mech, unitData);

        // Restore part states if present
        RestorePartStates(mech, unitData);

        mech.UpdateDestroyedStatus();
        
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
            var component = _componentProvider.CreateComponent(componentData.Type, componentData);
            if (component == null)
                continue;

            // Group assignments by location
            var groupedAssignments = componentData.Assignments
                .GroupBy(a => a.Location);

            foreach (var group in groupedAssignments)
            {
                var part = mech.Parts[group.Key];

                // Merge all slots from assignments for this location
                var slots = group.SelectMany(a => a.GetSlots())
                    .Distinct()
                    .ToArray();

                // Mount the component to this location once
                part.TryAddComponent(component, slots);
            }
        }
    }

    private void RestorePartStates(Mech mech, UnitData unitData)
    {
        // If no part states are provided, the unit is pristine (no damage)
        if (unitData.State?.UnitPartStates == null || unitData.State?.UnitPartStates.Count == 0)
            return;

        foreach (var partState in unitData.State?.UnitPartStates??[])
        {
            if (!mech.Parts.TryGetValue(partState.Location, out var part))
                continue;

            // Get current values (use max if not specified in the state)
            var currentFrontArmor = partState.CurrentFrontArmor ?? part.MaxArmor;
            var currentStructure = partState.CurrentStructure ?? part.MaxStructure;
            var isBlownOff = partState.IsBlownOff;

            // Restore state based on a part type
            if (part is Torso torso)
            {
                var currentRearArmor = partState.CurrentRearArmor ?? torso.MaxRearArmor;
                torso.RestoreTorsoState(currentFrontArmor, currentRearArmor, currentStructure, isBlownOff);
            }
            else
            {
                part.RestoreState(currentFrontArmor, currentStructure, isBlownOff);
            }
        }
    }
}
