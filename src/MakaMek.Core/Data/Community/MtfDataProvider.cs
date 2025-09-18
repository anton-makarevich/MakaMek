using System.Text.RegularExpressions;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Community;

public class MtfDataProvider:IMechDataProvider
{
    public UnitData LoadMechFromTextData(IEnumerable<string> lines)
    {
        var listLines = lines.ToList();
        var mechData = ParseBasicData(listLines);
        var (equipment, armorValues) = ParseLocationData(listLines);

        return new UnitData
        {
            Chassis = mechData["chassis"],
            Model = mechData["model"],
            Mass = int.Parse(mechData["Mass"]),
            WalkMp = int.Parse(Regex.Match(mechData["Walk MP"], @"\d+").Value),
            EngineRating = int.Parse(mechData["EngineRating"]),
            EngineType = mechData["EngineType"],
            ArmorValues = armorValues,
            Equipment = equipment,
            Quirks = mechData.Where(pair => pair.Key.StartsWith("quirk")).ToDictionary(),
            AdditionalAttributes = mechData.Where(pair => pair.Key.StartsWith("system")).ToDictionary()
        };
    }

    private Dictionary<string, string> ParseBasicData(IEnumerable<string> lines)
    {
        var mechData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var quirksCount = 0;
        var systemsCount = 0;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("Config:"))
                continue;

            var colonIndex = line.IndexOf(':');
            if (colonIndex <= 0) continue;
            var key = line[..colonIndex].Trim();
            var value = line[(colonIndex + 1)..].Trim();

            if (key == "Engine")
            {
                var engineData = value.Split(' ');
                if (engineData.Length >= 2)
                {
                    mechData["EngineRating"] = engineData[0];
                    mechData["EngineType"] = engineData[1];
                }
            }
            else
            {
                if (key.StartsWith("quirk"))
                {
                    key = $"{key}{++quirksCount}";
                }
                if (key.StartsWith("system"))
                {
                    key = $"{key}{++systemsCount}";
                }
                mechData[key] = value;
            }
        }
        return mechData;
    }

    private (List<ComponentData> equipment, Dictionary<PartLocation, ArmorLocation> armor) ParseLocationData(IEnumerable<string> lines)
    {
        // Track components by location and slot for consolidation
        var locationSlotComponents = new Dictionary<PartLocation, Dictionary<int, MakaMekComponent>>();
        var armorValues = new Dictionary<PartLocation, ArmorLocation>();
        PartLocation? currentLocation = null;
        var currentSlotIndex = 0;
        var parsingArmor = false;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (currentLocation == PartLocation.RightLeg)
                {
                    break; // End of equipment data
                }
                continue;
            }

            // Start of armor section
            if (line.StartsWith("Armor:"))
            {
                parsingArmor = true;
                continue;
            }

            // End of armor section
            if (line.StartsWith("Weapons:"))
            {
                parsingArmor = false;
                continue;
            }

            // Parse armor values
            if (parsingArmor)
            {
                var match = Regex.Match(line, @"(\w+)\s+Armor:(\d+)");
                if (match.Success && TryParseLocation(match.Groups[1].Value, out var location))
                {
                    var value = int.Parse(match.Groups[2].Value);
                    if (!armorValues.ContainsKey(location))
                        armorValues[location] = new ArmorLocation();

                    // Handle rear armor values
                    if (IsRearArmor(match.Groups[1].Value))
                    {
                        var mainLocation = GetMainLocationForRear(match.Groups[1].Value);
                        if (!armorValues.ContainsKey(mainLocation))
                            armorValues[mainLocation] = new ArmorLocation();
                        armorValues[mainLocation].RearArmor = value;
                    }
                    else
                    {
                        armorValues[location].FrontArmor = value;
                    }
                }
                continue;
            }

            // Check for location headers
            if (line.EndsWith(":"))
            {
                var locationText = line[..^1].Trim();
                if (TryParseLocation(locationText, out var location))
                {
                    currentLocation = location;
                    currentSlotIndex = 0; // Reset slot index for new location
                    if (!locationSlotComponents.ContainsKey(location))
                        locationSlotComponents[location] = new Dictionary<int, MakaMekComponent>();
                }
                continue;
            }

            // Add equipment to current location with slot tracking
            if (currentLocation.HasValue)
            {
                if (!line.Contains("-Empty-"))
                {
                    var component = MapMtfStringToComponent(line);
                    locationSlotComponents[currentLocation.Value][currentSlotIndex] = component;
                }
                currentSlotIndex++; // Increment slot index for each line (including empty slots)
            }
        }

        // Convert location-slot data to component-centric data
        var equipment = ConvertToComponentData(locationSlotComponents);
        return (equipment, armorValues);
    }

    /// <summary>
    /// Converts location-slot component data to component-centric ComponentData objects
    /// </summary>
    private List<ComponentData> ConvertToComponentData(Dictionary<PartLocation, Dictionary<int, MakaMekComponent>> locationSlotComponents)
    {
        var componentDataList = new List<ComponentData>();
        var processedSlots = new HashSet<(PartLocation, int)>(); // Track processed slots

        foreach (var (location, slotComponents) in locationSlotComponents)
        {
            foreach (var (slot, component) in slotComponents.OrderBy(kvp => kvp.Key))
            {
                if (processedSlots.Contains((location, slot)))
                    continue;

                // Find all consecutive slots with the same component in this location
                var assignments = new List<LocationSlotAssignment>();
                var currentSlot = slot;
                var consecutiveSlots = 1;

                // Mark this slot as processed
                processedSlots.Add((location, currentSlot));

                // Check for consecutive slots with the same component
                while (slotComponents.TryGetValue(currentSlot + consecutiveSlots, out var nextComponent) &&
                       nextComponent == component)
                {
                    processedSlots.Add((location, currentSlot + consecutiveSlots));
                    consecutiveSlots++;
                }

                // Create assignment for this consecutive block
                assignments.Add(new LocationSlotAssignment(location, currentSlot, consecutiveSlots));

                if (IsMultiLocationComponent(component))
                {
                    // Check if this component appears in other locations (multi-location component)
                    foreach (var (otherLocation, otherSlotComponents) in locationSlotComponents)
                    {
                        foreach (var (otherSlot, otherComponent) in otherSlotComponents.OrderBy(kvp => kvp.Key))
                        {
                            if (otherComponent != component || processedSlots.Contains((otherLocation, otherSlot)))
                                continue;
                            // Find consecutive slots in this other location
                            var otherCurrentSlot = otherSlot;
                            var otherConsecutiveSlots = 1;
                            processedSlots.Add((otherLocation, otherCurrentSlot));

                            while (otherSlotComponents.TryGetValue(otherCurrentSlot + otherConsecutiveSlots,
                                       out var otherNextComponent) &&
                                   otherNextComponent == component)
                            {
                                processedSlots.Add((otherLocation, otherCurrentSlot + otherConsecutiveSlots));
                                otherConsecutiveSlots++;
                            }

                            assignments.Add(new LocationSlotAssignment(otherLocation, otherCurrentSlot,
                                otherConsecutiveSlots));
                        }
                    }
                }

                // Create ComponentData for this component instance
                componentDataList.Add(new ComponentData
                {
                    Type = component,
                    Assignments = assignments
                });
            }
        }

        return componentDataList;
    }

    private static bool IsMultiLocationComponent(MakaMekComponent component)
    {
        return component== MakaMekComponent.Engine;
    }

    private MakaMekComponent MapMtfStringToComponent(string mtfString)
    {
        return mtfString switch
        {
            "IS Ammo AC/5" => MakaMekComponent.ISAmmoAC5,
            "IS Ammo SRM-2" => MakaMekComponent.ISAmmoSRM2,
            "IS Ammo MG - Full" => MakaMekComponent.ISAmmoMG,
            "IS Ammo LRM-5" => MakaMekComponent.ISAmmoLRM5,
            "Medium Laser" => MakaMekComponent.MediumLaser,
            "LRM 5" => MakaMekComponent.LRM5,
            "SRM 2" => MakaMekComponent.SRM2,
            "Machine Gun" => MakaMekComponent.MachineGun,
            "Autocannon/5" => MakaMekComponent.AC5,
            "Heat Sink" => MakaMekComponent.HeatSink,
            "Shoulder" => MakaMekComponent.Shoulder,
            "Upper Arm Actuator" => MakaMekComponent.UpperArmActuator,
            "Lower Arm Actuator" => MakaMekComponent.LowerArmActuator,
            "Hand Actuator" => MakaMekComponent.HandActuator,
            "Jump Jet" => MakaMekComponent.JumpJet,
            "Fusion Engine" => MakaMekComponent.Engine,
            "Gyro" => MakaMekComponent.Gyro,
            "Life Support" => MakaMekComponent.LifeSupport,
            "Sensors" => MakaMekComponent.Sensors,
            "Cockpit" => MakaMekComponent.Cockpit,
            "Hip" => MakaMekComponent.Hip,
            "Upper Leg Actuator" => MakaMekComponent.UpperLegActuator,
            "Lower Leg Actuator" => MakaMekComponent.LowerLegActuator,
            "Foot Actuator" => MakaMekComponent.FootActuator,
            _ => throw new NotImplementedException($"Unknown MTF component: {mtfString}")
        };
    }

    private static bool TryParseLocation(string locationText, out PartLocation location)
    {
        location = locationText switch
        {
            "Left Arm" or "LA" => PartLocation.LeftArm,
            "Right Arm" or "RA" => PartLocation.RightArm,
            "Left Torso" or "LT" => PartLocation.LeftTorso,
            "Right Torso" or "RT" => PartLocation.RightTorso,
            "Center Torso" or "CT" => PartLocation.CenterTorso,
            "Head" or "HD" => PartLocation.Head,
            "Left Leg" or "LL" => PartLocation.LeftLeg,
            "Right Leg" or "RL" => PartLocation.RightLeg,
            "RTL" or "RTR" or "RTC" => GetMainLocationForRear(locationText),
            _ => throw new ArgumentException($"Unknown location: {locationText}")
        };
        return true;
    }

    private static bool IsRearArmor(string locationText)
    {
        return locationText is "RTL" or "RTR" or "RTC";
    }

    private static PartLocation GetMainLocationForRear(string rearLocationText) => rearLocationText switch
    {
        "RTL" => PartLocation.LeftTorso,
        "RTR" => PartLocation.RightTorso,
        "RTC" => PartLocation.CenterTorso,
        _ => throw new ArgumentException($"Invalid rear location: {rearLocationText}")
    };
}