using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

public record TerrainRollModifier : RollModifier
{
    public required HexCoordinates Location { get; init; }
    public required MakaMekTerrains TerrainId { get; init; }

    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_Terrain"), 
            TerrainId, Location, Value);
}
