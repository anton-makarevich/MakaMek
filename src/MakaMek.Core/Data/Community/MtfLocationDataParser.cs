using System.Text.RegularExpressions;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;

namespace Sanet.MakaMek.Core.Data.Community;

/// <summary>
/// Parses the location, armor and equipment sections of an MTF file into per-location slot
/// assignments and armor values, then converts the slots into component-centric data. Extracted
/// from <see cref="MtfDataProvider"/> so each section is parsed by its own method instead of a
/// single nested condition ladder.
/// </summary>
internal sealed partial class MtfLocationDataParser
{
    private readonly IComponentProvider _componentProvider;
    private readonly Dictionary<PartLocation, SortedDictionary<int, (MakaMekComponent component, ComponentOptions options)>>
        _locationSlotComponents = new();
    private readonly Dictionary<PartLocation, ArmorLocation> _armorValues = new();

    private PartLocation? _currentLocation;
    private int _currentSlotIndex;
    private bool _parsingArmor;

    [GeneratedRegex(@"(\w+)\s+Armor:(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ArmorValuePattern();

    private MtfLocationDataParser(IComponentProvider componentProvider)
    {
        _componentProvider = componentProvider;
    }

    public static (List<ComponentData> equipment, Dictionary<PartLocation, ArmorLocation> armor) Parse(
        IEnumerable<string> lines,
        Dictionary<string, string> mechData,
        IComponentProvider componentProvider)
    {
        var parser = new MtfLocationDataParser(componentProvider);
        foreach (var line in lines)
        {
            if (!parser.Accept(line)) break;
        }

        return (parser.ConvertToComponentData(mechData), parser._armorValues);
    }

    /// <summary>
    /// Feeds one line to the section currently being parsed.
    /// </summary>
    /// <returns><see langword="false"/> once the equipment section has ended.</returns>
    private bool Accept(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            // True for mtf, but maybe should find a better way
            return _currentLocation != PartLocation.RightLeg;
        }

        // Start of the armor section
        if (line.StartsWith("Armor:", StringComparison.OrdinalIgnoreCase))
        {
            _parsingArmor = true;
            return true;
        }

        // End of the armor section
        if (line.StartsWith("Weapons:", StringComparison.OrdinalIgnoreCase))
        {
            _parsingArmor = false;
            return true;
        }

        if (_parsingArmor)
        {
            ParseArmorLine(line);
            return true;
        }

        if (line.EndsWith(':'))
        {
            StartLocation(line[..^1].Trim());
            return true;
        }

        ParseEquipmentLine(line);
        return true;
    }

    private void ParseArmorLine(string line)
    {
        var match = ArmorValuePattern().Match(line);
        if (!match.Success) return;

        var locationText = match.Groups[1].Value;
        var location = MtfComponentMapping.ParseLocation(locationText);
        var value = int.Parse(match.Groups[2].Value);

        if (!_armorValues.ContainsKey(location))
            _armorValues[location] = new ArmorLocation();

        // Rear armor is stored on the main location, so a rear line both creates and fills it.
        if (MtfComponentMapping.IsRearArmor(locationText))
        {
            var mainLocation = MtfComponentMapping.GetMainLocationForRear(locationText);
            if (!_armorValues.ContainsKey(mainLocation))
                _armorValues[mainLocation] = new ArmorLocation();
            _armorValues[mainLocation].RearArmor = value;
            return;
        }

        _armorValues[location].FrontArmor = value;
    }

    private void StartLocation(string locationText)
    {
        if (!MtfComponentMapping.TryParseLocation(locationText, out var location)) return;

        _currentLocation = location;
        _currentSlotIndex = 0; // Reset slot index for a new location
        if (!_locationSlotComponents.ContainsKey(location))
            _locationSlotComponents[location] = new SortedDictionary<int, (MakaMekComponent, ComponentOptions)>();
    }

    /// <summary>
    /// Adds equipment to the current location, tracking the slot index of every line including
    /// the empty ones.
    /// </summary>
    private void ParseEquipmentLine(string line)
    {
        if (_currentLocation is not { } currentLocation) return;

        if (!line.Contains("-Empty-", StringComparison.OrdinalIgnoreCase))
        {
            var (componentName, options) = ParseEquipmentOptions(line);
            var component = MtfComponentMapping.MapMtfStringToComponent(componentName);
            _locationSlotComponents[currentLocation][_currentSlotIndex] = (component, options);
        }

        _currentSlotIndex++;
    }

    private static (string name, ComponentOptions options) ParseEquipmentOptions(string line)
    {
        var componentName = line;
        var options = new ComponentOptions();

        // Check for rear-facing suffix
        if (componentName.EndsWith(" (R)", StringComparison.OrdinalIgnoreCase))
        {
            options = options with { IsRearFacing = true };
            componentName = componentName[..^4]; // Strip " (R)" suffix
        }

        if (componentName.EndsWith(" - Full", StringComparison.OrdinalIgnoreCase))
            componentName = componentName[..^7];

        if (componentName.EndsWith(" - Half", StringComparison.OrdinalIgnoreCase))
        {
            componentName = componentName[..^7];
            options = options with { MassRoundsMultiplier = 0.5m };
        }

        return (componentName, options);
    }

    /// <summary>
    /// Converts location-slot component data to component-centric ComponentData objects
    /// </summary>
    private List<ComponentData> ConvertToComponentData(Dictionary<string, string> mechData)
    {
        var componentDataList = new List<ComponentData>();
        var processedSlots = new HashSet<(PartLocation, int)>(); // Track processed slots

        foreach (var (location, slotComponents) in _locationSlotComponents)
        {
            foreach (var (slot, (component, options)) in slotComponents)
            {
                if (processedSlots.Contains((location, slot)))
                    continue;

                // Get component size and specific data
                var (componentSize, specificData) = GetComponentSizeAndData(component, mechData, options);

                // Collect all slot assignments for this component instance using size-based validation
                var assignments = CollectSlotAssignments(component, location, slot, processedSlots, componentSize);

                // Create ComponentData for this component instance
                componentDataList.Add(new ComponentData
                {
                    Type = component,
                    Assignments = assignments,
                    SpecificData = specificData
                });
            }
        }

        return componentDataList;
    }

    /// <summary>
    /// Gets the component size and specific data for the given component type
    /// </summary>
    private (int size, ComponentSpecificData? specificData) GetComponentSizeAndData(
        MakaMekComponent component,
        Dictionary<string, string> mechData,
        ComponentOptions? options = null)
    {
        var specificData = GetSpecificData(component, mechData, options);
        var definition = _componentProvider.GetDefinition(component, specificData);
        return definition == null
            ? throw new ArgumentException($"No definition found for component: {component}")
            : (definition.Size, specificData);
    }

    private static ComponentSpecificData? GetSpecificData(
        MakaMekComponent component,
        Dictionary<string, string> mechData,
        ComponentOptions? options)
    {
        if (component == MakaMekComponent.Engine
            && mechData.TryGetValue("engine-rating", out var ratingStr)
            && mechData.TryGetValue("engine-type", out var typeStr)
            && int.TryParse(ratingStr, out var rating)
            && Enum.TryParse<EngineType>(typeStr, true, out var engineType))
        {
            return new EngineStateData(engineType, rating);
        }

        // Handle rear-facing weapons
        if (options?.IsRearFacing == true)
            return new WeaponStateData(MountingOptions.Rear);

        // Handle ammo mass/rounds multiplier
        return options is { MassRoundsMultiplier: not 1m }
            ? new AmmoStateData(null, options.MassRoundsMultiplier)
            : null;
    }

    /// <summary>
    /// Collects slot assignments for a single component instance using size-based validation
    /// </summary>
    private List<LocationSlotAssignment> CollectSlotAssignments(
        MakaMekComponent component,
        PartLocation startLocation,
        int startSlot,
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
        var currentAssignments = FindConsecutiveSlotsInLocation(component, startLocation, startSlot, processedSlots, expectedSize);
        assignments.AddRange(currentAssignments);
        totalAssignedSlots += currentAssignments.Sum(a => a.Length);

        // Continue searching other locations until we have enough slots
        while (totalAssignedSlots < expectedSize)
        {
            var foundAdditional = AppendAssignmentsFromOtherLocations(
                component, processedSlots, expectedSize, assignments, ref totalAssignedSlots);
            // If we couldn't find any more slots, break to avoid infinite loop
            if (!foundAdditional) break;
        }

        return assignments;
    }

    private bool AppendAssignmentsFromOtherLocations(
        MakaMekComponent component,
        HashSet<(PartLocation, int)> processedSlots,
        int expectedSize,
        List<LocationSlotAssignment> assignments,
        ref int totalAssignedSlots)
    {
        var foundAdditional = false;

        foreach (var (location, slotComponents) in _locationSlotComponents)
        {
            if (totalAssignedSlots >= expectedSize) break;

            foreach (var (slot, (slotComponent, _)) in slotComponents)
            {
                if (slotComponent != component || processedSlots.Contains((location, slot)))
                    continue;

                var remainingSlots = expectedSize - totalAssignedSlots;
                var additionalAssignments = FindConsecutiveSlotsInLocation(
                    component, location, slot, processedSlots, remainingSlots);
                assignments.AddRange(additionalAssignments);
                totalAssignedSlots += additionalAssignments.Sum(a => a.Length);
                foundAdditional = true;

                if (totalAssignedSlots >= expectedSize)
                    break;
            }
        }

        return foundAdditional;
    }

    /// <summary>
    /// Finds consecutive slots with the same component in a specific location, up to maxSlots
    /// </summary>
    private List<LocationSlotAssignment> FindConsecutiveSlotsInLocation(
        MakaMekComponent component,
        PartLocation location,
        int startSlot,
        HashSet<(PartLocation, int)> processedSlots,
        int maxSlots = int.MaxValue) =>
        FindConsecutiveSlots(component, location, startSlot, _locationSlotComponents, processedSlots, maxSlots);

    internal static List<LocationSlotAssignment> FindConsecutiveSlots(
        MakaMekComponent component,
        PartLocation location,
        int startSlot,
        Dictionary<PartLocation, SortedDictionary<int, (MakaMekComponent component, ComponentOptions options)>> locationSlotComponents,
        HashSet<(PartLocation, int)> processedSlots,
        int maxSlots = int.MaxValue)
    {
        var assignments = new List<LocationSlotAssignment>();

        if (!locationSlotComponents.TryGetValue(location, out var slotComponents) ||
            !slotComponents.TryGetValue(startSlot, out var slotData) ||
            slotData.component != component ||
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
               slotComponents.TryGetValue(currentSlot + consecutiveSlots, out var nextSlotData) &&
               nextSlotData.component == component)
        {
            processedSlots.Add((location, currentSlot + consecutiveSlots));
            consecutiveSlots++;
        }

        // Create an assignment for this consecutive block
        assignments.Add(new LocationSlotAssignment(location, currentSlot, consecutiveSlots));

        return assignments;
    }
}
