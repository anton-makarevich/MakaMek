using Sanet.Localization;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using System.Globalization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

public record TerrainRollModifier : RollModifier
{
    public required HexCoordinates Location { get; init; }
    public required MakaMekTerrains TerrainId { get; init; }

    public override string Render(ILocalizationService localizationService) =>
        string.Format(CultureInfo.InvariantCulture, localizationService.GetString("Modifier_Terrain"), 
            TerrainId, Location, Value);
}
