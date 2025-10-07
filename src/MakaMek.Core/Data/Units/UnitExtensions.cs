using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Data.Units;

public static class UnitExtensions
{
    /// <summary>
    /// Converts a Unit to a UnitData object for serialization or storage
    /// </summary>
    /// <param name="unit">The unit to convert</param>
    /// <returns>A UnitData object representing the unit</returns>
    public static UnitData ToData(this Unit unit)
    {
        // Create armor values dictionary
        var armorValues = new Dictionary<PartLocation, ArmorLocation>();
        foreach (var part in unit.Parts.Values)
        {
            var armorLocation = part switch
            {
                SideTorso sideTorso => new ArmorLocation
                {
                    FrontArmor = sideTorso.MaxArmor,
                    RearArmor = sideTorso.MaxRearArmor
                },
                CenterTorso centerTorso => new ArmorLocation
                {
                    FrontArmor = centerTorso.MaxArmor,
                    RearArmor = centerTorso.MaxRearArmor
                },
                _ => new ArmorLocation { FrontArmor = part.MaxArmor, RearArmor = 0 }
            };

            armorValues[part.Location] = armorLocation;
        }

        // Get engine data
        var engine = unit.GetAllComponents<Engine>().FirstOrDefault();
        var engineRating = engine?.Rating ?? 0;
        var engineType = engine?.Type.ToString() ?? "Fusion";

        // Create component-centric equipment list
        var equipment = new List<ComponentData>();
        var processedComponents = new HashSet<Component>();

        foreach (var part in unit.Parts.Values)
        {
            // Filter out automatically added components
            var filteredComponents = part.Components
                .Where(c => c.IsRemovable && !processedComponents.Contains(c))
                .ToList();

            foreach (var component in filteredComponents)
            {
                // Mark component as processed to avoid duplicates for multi-location components
                processedComponents.Add(component);

                equipment.Add(component.ToData());
            }
        }

        // Serialize part states only for damaged/destroyed/blown-off parts
        var partStates = new List<UnitPartStateData>();
        foreach (var part in unit.Parts.Values)
        {
            // Check if part has any damage or is blown off
            var hasDamage = part.CurrentArmor < part.MaxArmor
                            || part.CurrentStructure < part.MaxStructure
                            || part.IsBlownOff;

            // For torso parts, also check rear armor
            if (part is Torso torso)
            {
                hasDamage = hasDamage || torso.CurrentRearArmor < torso.MaxRearArmor;
            }

            if (!hasDamage) continue;

            // Create part state data
            var partState = new UnitPartStateData
            {
                Location = part.Location,
                CurrentFrontArmor = part.CurrentArmor,
                CurrentStructure = part.CurrentStructure,
                IsBlownOff = part.IsBlownOff
            };

            // Add rear armor for torso parts
            if (part is Torso torsoWithRear)
            {
                partState = partState with { CurrentRearArmor = torsoWithRear.CurrentRearArmor };
            }

            partStates.Add(partState);
        }

        return new UnitData
        {
            Id = unit.Id,
            Chassis = unit.Chassis,
            Model = unit.Model,
            Mass = unit.Tonnage,
            WalkMp = unit.GetMovementPoints(MovementType.Walk),
            EngineRating = engineRating,
            EngineType = engineType,
            ArmorValues = armorValues,
            Equipment = equipment,
            AdditionalAttributes = new Dictionary<string, string>(),
            Quirks = new Dictionary<string, string>(),
            UnitPartStates = partStates.Count > 0 ? partStates : null
        };
    }
}
