using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Combat.Modifiers;

public record TerrainAttackModifier : AttackModifier
{
    public required HexCoordinates Location { get; init; }
    public required MakaMekTerrains TerrainId { get; init; }

    public override string Format(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_Terrain"), 
            TerrainId, Location, Value);
}
