using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Units;

public static class UnitExtensions
{
    /// <param name="unit">The unit to clone</param>
    extension(IUnit unit)
    {
        /// <summary>
        /// Creates a deep copy of the unit by converting it to data and recreating it
        /// </summary>
        /// <param name="mechFactory">The factory to use for creating the cloned unit</param>
        /// <returns>A new unit instance with the same state as the original</returns>
        public Unit CloneUnit(IMechFactory mechFactory)
        {
            var data = unit.ToData();
            return mechFactory.Create(data);
        }

        /// <summary>
        /// Converts a Unit to a UnitData object for serialization or storage
        /// </summary>
        /// <returns>A UnitData object representing the unit</returns>
        public UnitData ToData()
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
                if (part.IsPristine) continue;
            
                var partState = part.ToData();

                partStates.Add(partState);
            }
            
            var statusFlags = Enum.GetValues<UnitStatus>()
                .Where(s => s is not UnitStatus.None and not UnitStatus.Active)
                .Where(s => (unit.Status & s) == s)
                .ToArray();
            
            var movementPathSegments = unit.MovementTaken?.ToData();

            return new UnitData
            {
                Id = unit.Id,
                Chassis = unit.Chassis,
                Model = unit.Model,
                Mass = unit.Tonnage,
                EngineRating = engineRating,
                EngineType = engineType,
                ArmorValues = armorValues,
                Equipment = equipment,
                AdditionalAttributes = new Dictionary<string, string>(),
                Quirks = new Dictionary<string, string>(),
                State = new UnitStateData
                {
                    UnitPartStates = partStates.Count > 0 ? partStates : null,
                    StatusFlags = statusFlags.Length > 0 ? statusFlags : null,
                    MovementPathSegments = movementPathSegments,
                    Position = unit.Position?.ToData(),
                    DeclaredWeaponTargets = unit.DeclaredWeaponTargets,
                    CurrentHeat = unit.CurrentHeat
                }
            };
        }
        
        public UnitTacticalRole GetTacticalRole()
        {
            // 1. Check for LRM Boat
            // Logic: Has 20+ LRM tubes
            var lrmTubes = unit.GetAvailableComponents<Weapon>()
                .Where(w => w.Type == WeaponType.Missile && w.Name.Contains("LRM"))
                .Sum(w => w.Clusters * w.ClusterSize);

            if (lrmTubes >= 20)
            {
                return UnitTacticalRole.LrmBoat;
            }

            var walkMp = unit.GetMovementPoints(MovementType.Walk);
            var jumpMp = unit.GetMovementPoints(MovementType.Jump);

            // 2. Scout
            if (walkMp >= 6)
            {
                return UnitTacticalRole.Scout;
            }

            // 3. Brawler / Trooper
            if (walkMp < 4)
            {
                return UnitTacticalRole.Brawler;
            }
        
            // Default to Trooper/Skirmisher for 4-5 MP
            if (jumpMp > 0)
            {
                return UnitTacticalRole.Jumper;
            }

            return UnitTacticalRole.Trooper;
        }
    }
}