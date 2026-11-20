using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Rules;
using System.Globalization;

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
            Mass = int.Parse(mechData["mass"], CultureInfo.InvariantCulture),
            EngineRating = int.Parse(mechData["engine-rating"], CultureInfo.InvariantCulture),
            EngineType = mechData["engine-type"],
            ArmorValues = armorValues,
            Equipment = equipment,
            Quirks = mechData.Where(pair => pair.Key.StartsWith("quirk", StringComparison.Ordinal)).ToDictionary(),
            AdditionalAttributes = mechData.Where(pair => pair.Key.StartsWith("system", StringComparison.Ordinal)).ToDictionary()
        };
    }
}
