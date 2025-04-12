using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using System.Reflection;

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
        foreach (var part in unit.Parts)
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
        
        // Create equipment dictionary
        var locationEquipment = new Dictionary<PartLocation, List<MakaMekComponent>>();
        
        // Define components to exclude (automatically added by part constructors)
        var excludedComponents = new HashSet<Type>
        {
            typeof(Shoulder) // Shoulder actuators are automatically added to arms
        };
        
        foreach (var part in unit.Parts)
        {
            var equipment = new List<MakaMekComponent>();
            
            // Filter out automatically added components
            var filteredComponents = part.Components
                .Where(c => !excludedComponents.Contains(c.GetType()))
                .ToList();
                
            foreach (var component in filteredComponents)
            {
                var componentType = GetMakaMekComponentType(component);
                if (componentType == null) continue;
                
                // For components that take multiple slots, add one entry per slot
                for (var i = 0; i < component.Size; i++)
                {
                    equipment.Add(componentType.Value);
                }
            }
            
            // Only add the location if it has equipment
            if (equipment.Count > 0)
            {
                locationEquipment[part.Location] = equipment;
            }
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
    
    /// <summary>
    /// Maps a Component to its corresponding MakaMekComponent enum value
    /// </summary>
    private static MakaMekComponent? GetMakaMekComponentType(Component component)
    {
        return component switch
        {
            Engine => MakaMekComponent.Engine,
            MediumLaser => MakaMekComponent.MediumLaser,
            LRM5 => MakaMekComponent.LRM5,
            SRM2 => MakaMekComponent.SRM2,
            MachineGun => MakaMekComponent.MachineGun,
            AC5 => MakaMekComponent.AC5,
            HeatSink => MakaMekComponent.HeatSink,
            JumpJets => MakaMekComponent.JumpJet,
            Shoulder => MakaMekComponent.Shoulder,
            UpperArmActuator => MakaMekComponent.UpperArmActuator,
            LowerArmActuator => MakaMekComponent.LowerArmActuator,
            HandActuator => MakaMekComponent.HandActuator,
            Ammo ammo => GetAmmoComponentType(ammo),
            _ => null
        };
    }
    
    /// <summary>
    /// Maps an Ammo component to its corresponding MakaMekComponent enum value
    /// </summary>
    private static MakaMekComponent? GetAmmoComponentType(Ammo ammo)
    {
        return ammo.Type switch
        {
            AmmoType.AC5 => MakaMekComponent.ISAmmoAC5,
            AmmoType.SRM2 => MakaMekComponent.ISAmmoSRM2,
            AmmoType.MachineGun => MakaMekComponent.ISAmmoMG,
            AmmoType.LRM5 => MakaMekComponent.ISAmmoLRM5,
            _ => null
        };
    }
}
