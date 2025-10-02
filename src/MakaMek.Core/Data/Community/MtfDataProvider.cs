using System.Text.RegularExpressions;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;

namespace Sanet.MakaMek.Core.Data.Community;

public class MtfDataProvider:IUnitDataProvider
{
    private readonly IComponentProvider _componentProvider;
    private readonly string[] _nicknamePatterns = 
    [
        @"^([^']+)'([^']+)'$",
        @"^([^(]+)\(([^)]+)\)$"
    ];

    public MtfDataProvider(IComponentProvider componentProvider)
    {
        _componentProvider = componentProvider;
    }
    public UnitData LoadMechFromTextData(IEnumerable<string> lines)
    {
        var listLines = lines.ToList();
        var mechData = ParseBasicData(listLines);
        var (equipment, armorValues) = ParseLocationData(listLines, mechData);

        // Extract model and nickname from model field (format: "MODEL 'NICKNAME'" or "MODEL (NICKNAME)")
        var model = mechData["model"];
        var nickname = mechData.GetValueOrDefault("nickname");

        return new UnitData
        {
            Chassis = mechData["chassis"],
            Model = model,
            Nickname = nickname,
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
                if (key.StartsWith("model"))
                {
                    var (model, nickname) = ExtractModelAndNickname(value);
                    mechData["model"] = model;
                    if (!string.IsNullOrEmpty(nickname))
                        mechData["nickname"] = nickname;
                    continue;
                }
                mechData[key] = value;
            }
        }
        return mechData;
    }
    
    private (string model, string? nickname) ExtractModelAndNickname(string modelNickname)
    {
        var model = modelNickname;
        string? nickname = null;
        // Check for nickname in single quotes (e.g., "VND-1AA 'Avenging Angel'")
        
        foreach (var pattern in _nicknamePatterns)
        {
            var match = Regex.Match(model, pattern);
            if (match.Success)
            {
                model = match.Groups[1].Value.Trim();
                nickname = match.Groups[2].Value.Trim();
                break;
            }
        }
        
        return (model, nickname);
    }

    private (List<ComponentData> equipment, Dictionary<PartLocation, ArmorLocation> armor) ParseLocationData(IEnumerable<string> lines, Dictionary<string, string> mechData)
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
                if (currentLocation == PartLocation.RightLeg) // True for mtf, but maybe should find a better way
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
                    currentSlotIndex = 0; // Reset slot index for a new location
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
        var equipment = ConvertToComponentData(locationSlotComponents, mechData);
        return (equipment, armorValues);
    }

    /// <summary>
    /// Converts location-slot component data to component-centric ComponentData objects
    /// </summary>
    private List<ComponentData> ConvertToComponentData(Dictionary<PartLocation, Dictionary<int, MakaMekComponent>> locationSlotComponents, Dictionary<string, string> mechData)
    {
        var componentDataList = new List<ComponentData>();
        var processedSlots = new HashSet<(PartLocation, int)>(); // Track processed slots

        foreach (var (location, slotComponents) in locationSlotComponents)
        {
            foreach (var (slot, component) in slotComponents.OrderBy(kvp => kvp.Key))
            {
                if (processedSlots.Contains((location, slot)))
                    continue;

                // Get component size and specific data
                var (componentSize, specificData) = GetComponentSizeAndData(component, mechData);

                // Collect all slot assignments for this component instance using size-based validation
                var assignments = CollectSlotAssignments(component, location, slot, locationSlotComponents, processedSlots, componentSize);

                // Create ComponentData for this component instance
                var componentData = new ComponentData
                {
                    Type = component,
                    Assignments = assignments,
                    SpecificData = specificData
                };

                componentDataList.Add(componentData);
            }
        }

        return componentDataList;
    }

    /// <summary>
    /// Gets the component size and specific data for the given component type
    /// </summary>
    private (int size, ComponentSpecificData? specificData) GetComponentSizeAndData(MakaMekComponent component, Dictionary<string, string> mechData)
    {
        ComponentSpecificData? specificData = null;

        // Handle engine special case
        if (component == MakaMekComponent.Engine)
        {
            if (mechData.TryGetValue("EngineRating", out var ratingStr) &&
                mechData.TryGetValue("EngineType", out var typeStr) &&
                int.TryParse(ratingStr, out var rating) &&
                Enum.TryParse<EngineType>(typeStr, true, out var engineType))
            {
                specificData = new EngineStateData(engineType, rating);
            }
        }

        var definition = _componentProvider.GetDefinition(component, specificData);
        return definition == null 
            ? throw new ArgumentException($"No definition found for component: {component}") 
            : (definition.Size, specificData);
    }

    /// <summary>
    /// Collects slot assignments for a single component instance using size-based validation
    /// </summary>
    private List<LocationSlotAssignment> CollectSlotAssignments(
        MakaMekComponent component,
        PartLocation startLocation,
        int startSlot,
        Dictionary<PartLocation, Dictionary<int, MakaMekComponent>> locationSlotComponents,
        HashSet<(PartLocation, int)> processedSlots,
        int expectedSize)
    {
        var assignments = new List<LocationSlotAssignment>();
        var totalAssignedSlots = 0;

        // For single-slot components, just assign this one slot
        if (expectedSize == 1)
        {
            processedSlots.Add((startLocation, startSlot));
            assignments.Add(new LocationSlotAssignment(startLocation, startSlot, 1));
            return assignments;
        }

        // For multi-slot components, collect consecutive slots up to the expected size
        var currentAssignments = FindConsecutiveSlotsInLocation(component, startLocation, startSlot, locationSlotComponents, processedSlots, expectedSize - totalAssignedSlots);
        assignments.AddRange(currentAssignments);
        totalAssignedSlots += currentAssignments.Sum(a => a.Length);

        // Continue searching other locations until we have enough slots
        while (totalAssignedSlots < expectedSize)
        {
            var foundAdditional = false;

            foreach (var (location, slotComponents) in locationSlotComponents)
            {
                if (totalAssignedSlots >= expectedSize) break;

                foreach (var (slot, slotComponent) in slotComponents.OrderBy(kvp => kvp.Key))
                {
                    if (slotComponent != component || processedSlots.Contains((location, slot)))
                        continue;

                    var remainingSlots = expectedSize - totalAssignedSlots;
                    var additionalAssignments = FindConsecutiveSlotsInLocation(component, location, slot, locationSlotComponents, processedSlots, remainingSlots);
                    assignments.AddRange(additionalAssignments);
                    totalAssignedSlots += additionalAssignments.Sum(a => a.Length);
                    foundAdditional = true;

                    if (totalAssignedSlots >= expectedSize) 
                        break;
                }

                if (totalAssignedSlots >= expectedSize) break;
            }

            // If we couldn't find any more slots, break to avoid infinite loop
            if (!foundAdditional) break;
        }

        return assignments;
    }

    /// <summary>
    /// Finds consecutive slots with the same component in a specific location, up to maxSlots
    /// </summary>
    private List<LocationSlotAssignment> FindConsecutiveSlotsInLocation(
        MakaMekComponent component,
        PartLocation location,
        int startSlot,
        Dictionary<PartLocation, Dictionary<int, MakaMekComponent>> locationSlotComponents,
        HashSet<(PartLocation, int)> processedSlots,
        int maxSlots = int.MaxValue)
    {
        var assignments = new List<LocationSlotAssignment>();

        if (!locationSlotComponents.TryGetValue(location, out var slotComponents) ||
            !slotComponents.TryGetValue(startSlot, out var slotComponent) ||
            slotComponent != component ||
            processedSlots.Contains((location, startSlot)))
        {
            return assignments;
        }

        var currentSlot = startSlot;
        var consecutiveSlots = 1;

        // Mark this slot as processed
        processedSlots.Add((location, currentSlot));

        // Check for consecutive slots with the same component, up to maxSlots
        while (consecutiveSlots < maxSlots &&
               slotComponents.TryGetValue(currentSlot + consecutiveSlots, out var nextComponent) &&
               nextComponent == component)
        {
            processedSlots.Add((location, currentSlot + consecutiveSlots));
            consecutiveSlots++;
        }

        // Create an assignment for this consecutive block
        assignments.Add(new LocationSlotAssignment(location, currentSlot, consecutiveSlots));

        return assignments;
    }

    private static MakaMekComponent MapMtfStringToComponent(string mtfString)
    {
        return mtfString switch
        {
            "IS Ammo AC/2" => MakaMekComponent.ISAmmoAC2,
            "IS Ammo AC/5" => MakaMekComponent.ISAmmoAC5,
            "IS Ammo AC/10" => MakaMekComponent.ISAmmoAC10,
            "IS Ammo AC/20" => MakaMekComponent.ISAmmoAC20,
            "IS Ammo SRM-2" => MakaMekComponent.ISAmmoSRM2,
            "IS Ammo SRM-4" => MakaMekComponent.ISAmmoSRM4,
            "IS Ammo SRM-6" => MakaMekComponent.ISAmmoSRM6,
            "IS Ammo MG - Full" => MakaMekComponent.ISAmmoMG,
            "IS Ammo LRM-5" => MakaMekComponent.ISAmmoLRM5,
            "IS Ammo LRM-10" => MakaMekComponent.ISAmmoLRM10,
            "IS Ammo LRM-15" => MakaMekComponent.ISAmmoLRM15,
            "IS Ammo LRM-20" => MakaMekComponent.ISAmmoLRM20,
            "Small Laser" => MakaMekComponent.SmallLaser,
            "Medium Laser" => MakaMekComponent.MediumLaser,
            "Large Laser" => MakaMekComponent.LargeLaser,
            "PPC" => MakaMekComponent.PPC,
            "Flamer" => MakaMekComponent.Flamer,
            "LRM 5" => MakaMekComponent.LRM5,
            "LRM 10" => MakaMekComponent.LRM10,
            "LRM 15" => MakaMekComponent.LRM15,
            "LRM 20" => MakaMekComponent.LRM20,
            "SRM 2" => MakaMekComponent.SRM2,
            "SRM 4" => MakaMekComponent.SRM4,
            "SRM 6" => MakaMekComponent.SRM6,
            "Machine Gun" => MakaMekComponent.MachineGun,
            "Autocannon/2" => MakaMekComponent.AC2,
            "Autocannon/5" => MakaMekComponent.AC5,
            "Autocannon/10" => MakaMekComponent.AC10,
            "Autocannon/20" => MakaMekComponent.AC20,
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