using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Community;

public class MtfDataProvider : IUnitDataProvider
{
    private readonly IComponentProvider _componentProvider;

    public MtfDataProvider(IComponentProvider componentProvider)
    {
        _componentProvider = componentProvider;
    }

    public UnitData LoadMechFromTextData(IEnumerable<string> lines)
    {
        var listLines = lines.ToList();
        var mechData = MtfBasicDataParser.Parse(listLines);
        var (equipment, armorValues) = MtfLocationDataParser.Parse(listLines, mechData, _componentProvider);

        var model = mechData["model"];
        var nickname = mechData.GetValueOrDefault("nickname");

        return new UnitData
        {
            Chassis = mechData["chassis"],
            Model = model,
            Nickname = nickname,
            Mass = int.Parse(mechData["mass"]),
            EngineRating = int.Parse(mechData["engine-rating"]),
            EngineType = mechData["engine-type"],
            ArmorValues = armorValues,
            Equipment = equipment,
            Quirks = mechData.Where(pair => pair.Key.StartsWith("quirk", StringComparison.Ordinal)).ToDictionary(),
            AdditionalAttributes = mechData.Where(pair => pair.Key.StartsWith("system", StringComparison.Ordinal)).ToDictionary()
        };
    }

    /// <summary>
    /// Locates the consecutive slots of one component in one location. Kept here because the
    /// slot-consolidation rules are part of the provider's contract and are unit-tested directly.
    /// </summary>
    private List<LocationSlotAssignment> FindConsecutiveSlotsInLocation(
        MakaMekComponent component,
        PartLocation location,
        int startSlot,
        Dictionary<PartLocation, SortedDictionary<int, (MakaMekComponent component, ComponentOptions options)>> locationSlotComponents,
        HashSet<(PartLocation, int)> processedSlots,
        int maxSlots = int.MaxValue) =>
        MtfLocationDataParser.FindConsecutiveSlots(
            component, location, startSlot, locationSlotComponents, processedSlots, maxSlots);
}
