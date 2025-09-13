using Sanet.MakaMek.Core.Models.Units;
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
        
        // Create equipment dictionary with slot layouts
        var locationEquipment = new Dictionary<PartLocation, LocationSlotLayout>();

        foreach (var part in unit.Parts.Values)
        {
            var slotLayout = new LocationSlotLayout();

            // Filter out automatically added components
            var filteredComponents = part.Components
                .Where(c => c.IsRemovable)
                .ToList();

            foreach (var component in filteredComponents)
            {
                var assignmentsForThisLocation = component.SlotAssignments;

                foreach (var assignment in assignmentsForThisLocation)
                {
                    foreach (var slot in assignment.Slots)
                    {
                        slotLayout.AssignComponent(slot, component.ComponentType);
                    }
                }
            }

            // Always add the location (even if empty) to maintain consistency
            locationEquipment[part.Location] = slotLayout;
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
            LocationEquipment = locationEquipment,
            AdditionalAttributes = new Dictionary<string, string>(),
            Quirks = new Dictionary<string, string>()
        };
    }
}
